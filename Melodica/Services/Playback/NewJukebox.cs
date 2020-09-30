using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

using Discord;
using Discord.Audio;
using Discord.WebSocket;

using Melodica.Core.Exceptions;
using Melodica.Services.Audio;
using Melodica.Services.Downloaders.Exceptions;
using Melodica.Services.Models;
using Melodica.Services.Playback.Requests;

using Microsoft.EntityFrameworkCore.Internal;

namespace Melodica.Services.Playback
{
    public class NewJukebox
    {
        public NewJukebox(MediaCallback mediaCallback)
        {
            this.mediaCallback = mediaCallback;
        }

        public delegate void MediaCallback(MediaMetadata info, MediaState state, SubRequestInfo? subRequestInfo);

        public const int MaxVolume = 100;
        public const int MinVolume = 5;

        private bool loop;
        public bool Loop
        {
            get => loop;
            set
            {
                if (!Playing) return;
                loop = value;
            }
        }

        private bool paused;
        public bool Paused
        {
            get => paused;
            set
            {
                if (!Playing) return;
                paused = value;
            }
        }

        public bool Playing { get; private set; }

        public bool Shuffle { get; set; }

        public bool Repeat { get; set; }

        public SongQueue Queue { get; } = new SongQueue();

        public (MediaMetadata info, SubRequestInfo? subInfo)? CurrentSong { get; private set; }

        public TimeSpan Duration => new TimeSpan(durationTimer.Elapsed.Hours, durationTimer.Elapsed.Minutes, durationTimer.Elapsed.Seconds);

        IAudioClient? audioClient;
        readonly MediaCallback mediaCallback;
        readonly Stopwatch durationTimer = new Stopwatch();

        bool stopRequested = false;

        readonly SemaphoreSlim writeLock = new SemaphoreSlim(1);

        private Task SendDataAsync(ExternalAudioProcessor audioProcessor, int bitrate)
        {
            using var input = audioProcessor.GetOutput();
            using var output = audioClient!.CreatePCMStream(AudioApplication.Music, bitrate, 100, 0);

            var writeThread = new Thread(() =>
            {
                int count = 0;
                byte[] buffer = new byte[1024];
                try
                {
                    writeLock.Wait();
                    while ((count = input!.Read(buffer, 0, buffer.Length)) != 0)
                    {
                        while (Paused && !stopRequested) { }

                        if (stopRequested) break;

                        output.Write(buffer, 0, count);
                    }
                }
                catch { }
                finally { writeLock.Release(); }
            });            

            writeThread.Name = "AudioWriteThread";
            writeThread.IsBackground = false;
            writeThread.Priority = ThreadPriority.Highest;

            writeThread.Start();
            Playing = true;
            writeThread.Join();
            Playing = false;

            if (stopRequested) stopRequested = false;

            return Task.CompletedTask;
        }

        private async Task DisconnectAsync()
        {
            if (audioClient == null)
                return;
            await audioClient.StopAsync();
            audioClient = null;
        }

        private async Task ConnectAsync(IAudioChannel audioChannel)
        {
            if (audioClient != null && (audioClient.ConnectionState == ConnectionState.Connected || audioClient.ConnectionState == ConnectionState.Connecting))
                return;

            audioClient = await audioChannel.ConnectAsync();
        }

        public Task SkipAsync()
        {
            stopRequested = true;
            return Task.CompletedTask;
        }

        public async Task SwitchAsync(MediaRequest request, IAudioChannel audioChannel)
        {
            if (!Playing)
                return;

            MediaMetadata? requestInfo = null;
            PlayableMedia media;
            try
            {
                requestInfo = request.GetInfo();
                mediaCallback(requestInfo, MediaState.Downloading, null);

                media = await request.GetMediaAsync();
                CurrentSong = (media.Info, request.SubRequestInfo);                
            }
            catch (Exception ex)
            {
                if (requestInfo != null)
                    mediaCallback(requestInfo, MediaState.Error, request.SubRequestInfo);

                if (ex is MediaUnavailableException)
                    throw ex;

                if (Queue.IsEmpty) throw;
                else return;
            }

            await ConnectAsync(audioChannel);

            stopRequested = true;
            mediaCallback(media.Info, MediaState.Playing, request.SubRequestInfo);
            using var audioProcessor = new FFmpegAudioProcessor(media.Info.DataInformation.MediaPath ?? throw new NullReferenceException("MediaPath was null."), media.Info.DataInformation.Format);
            await SendDataAsync(audioProcessor, GetChannelBitrate(audioChannel));
            mediaCallback(media.Info, MediaState.Finished, request.SubRequestInfo);
        }

        async Task QueueAsync(MediaRequest request)
        {
            await Queue.EnqueueAsync(request);
        }

        int GetChannelBitrate(IAudioChannel channel)
        {
            return channel switch
            {
                SocketVoiceChannel sv => sv.Bitrate,
                _ => throw new CriticalException("Unable to get channel bitrate.")
            };
        }

        public async Task PlayAsync(MediaRequest request, IAudioChannel audioChannel)
        {
            //TODO: Finish implementation with same features from the old Jukebox.
            if (Playing)
            {
                await QueueAsync(request);
                return;
            }

            MediaMetadata? requestInfo = null;
            PlayableMedia media;
            try
            {
                requestInfo = request.GetInfo();
                mediaCallback(requestInfo, MediaState.Downloading, null);

                media = await request.GetMediaAsync();
                CurrentSong = (media.Info, request.SubRequestInfo);
            }
            catch (Exception ex)
            {
                if (requestInfo != null)
                    mediaCallback(requestInfo, MediaState.Error, request.SubRequestInfo);

                if (ex is MediaUnavailableException)
                    throw ex;

                if (Queue.IsEmpty) throw;
                else
                {
                    var next = Shuffle ? await Queue.DequeueRandomAsync() : await Queue.DequeueAsync();
                    await PlayAsync(next, audioChannel);
                    return;
                }
            }

            await ConnectAsync(audioChannel);

            mediaCallback(media.Info, MediaState.Playing, request.SubRequestInfo);
            using var audioProcessor = new FFmpegAudioProcessor(media.Info.DataInformation.MediaPath ?? throw new NullReferenceException("MediaPath was null."), media.Info.DataInformation.Format);
            await SendDataAsync(audioProcessor, GetChannelBitrate(audioChannel));
            mediaCallback(media.Info, MediaState.Finished, request.SubRequestInfo);

            if (!Queue.IsEmpty)
            {
                var next = Shuffle ? await Queue.DequeueRandomAsync() : await Queue.DequeueAsync();
                await PlayAsync(next, audioChannel).ConfigureAwait(false);
                return;
            }

            await DisconnectAsync().ConfigureAwait(false);
        }

        public async Task StopAsync()
        {
            if (audioClient == null)
                return;
            stopRequested = true;
            await audioClient.StopAsync();
            await Queue.ClearAsync();
            CurrentSong = null;
        }
    }
}
