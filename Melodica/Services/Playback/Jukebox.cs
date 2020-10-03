using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

using Discord;
using Discord.Audio;
using Discord.WebSocket;

using Melodica.Core.Exceptions;
using Melodica.Services.Audio;
using Melodica.Services.Downloaders.Exceptions;
using Melodica.Services.Models;
using Melodica.Services.Playback.Exceptions;
using Melodica.Services.Playback.Requests;
using Melodica.Utility.Extensions;

using Microsoft.EntityFrameworkCore.Internal;

namespace Melodica.Services.Playback
{
    public enum MediaState
    {
        Error,
        Queued,
        Downloading,
        Playing,
        Finished
    };

    public class Jukebox
    {
        public Jukebox(MediaCallback mediaCallback)
        {
            this.mediaCallback = mediaCallback;
        }

        public delegate void MediaCallback(MediaMetadata info, MediaState state, SubRequestInfo? subRequestInfo);

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

        public (MediaMetadata info, SubRequestInfo? subInfo)? Song { get; private set; }

        public TimeSpan Duration => new TimeSpan(durationTimer.Elapsed.Hours, durationTimer.Elapsed.Minutes, durationTimer.Elapsed.Seconds);

        IAudioClient? audioClient;
        readonly MediaCallback mediaCallback;
        readonly Stopwatch durationTimer = new Stopwatch();

        bool stopRequested = false;
        bool downloading = false;

        readonly SemaphoreSlim writeLock = new SemaphoreSlim(1);

        private async Task<bool> CheckIfAloneAsync(IAudioChannel channel)
        {
            var users = await channel.GetUsersAsync().FirstAsync();
            if (!users.IsOverSize(1))
                return true;
            else return false;
        }

        private Task SendDataAsync(ExternalAudioProcessor audioProcessor, IAudioChannel channel, int bitrate)
        {
            bool isAlone = false;
            bool BreakConditions() => stopRequested || isAlone;

            //TODO: Prevent the discord web expired exception from breaking the bot.
            var writeThread = new Thread(() =>
            {
                using var input = audioProcessor.GetOutput();
                using var output = audioClient!.CreatePCMStream(AudioApplication.Music, bitrate, 100, 0);
                
                using var aloneTimer = new Timer(x => isAlone = CheckIfAloneAsync(channel).Result, null, 0, 5000);

                int count = 0;
                byte[] buffer = new byte[1024];
                try
                {
                    durationTimer.Start();
                    writeLock.Wait();
                    while ((count = input!.Read(buffer, 0, buffer.Length)) != 0)
                    {
                        bool shouldBreak = BreakConditions();

                        while (Paused && !shouldBreak) { Thread.Sleep(1000); }

                        output.Write(buffer, 0, count);

                        if (shouldBreak) break;
                    }
                }
                catch { }
                finally
                {
                    output.Flush();
                    durationTimer.Reset();
                    writeLock.Release();
                }
            });

            writeThread.Name = "AudioWriteThread";
            writeThread.IsBackground = false;
            writeThread.Priority = ThreadPriority.Highest;
            
            writeThread.Start();
            Playing = true;
            writeThread.Join();
            Playing = false;

            if (isAlone)
            {
                DisconnectAsync().ConfigureAwait(false);
                throw new EmptyChannelException();
            }

            if (stopRequested) stopRequested = false;

            return Task.CompletedTask;
        }

        public async Task DisconnectAsync()
        {
            await StopAsync();

            Song = null;
            await Queue.ClearAsync();

            if (audioClient == null)
                return;
            await audioClient.StopAsync();
            audioClient = null;
        }

        private async Task StopAsync()
        {
            if (!Playing)
                return;
            stopRequested = true;
            await writeLock.WaitAsync();
            writeLock.Release();
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

        public async Task SwitchAsync(MediaRequest request)
        {
            if (!Playing)
                throw new Exception("No song is playing.");
            await Queue.PutFirstAsync(request);
            Shuffle = false;
            Loop = false;
            stopRequested = true;
        }

        async Task QueueAsync(MediaRequest request)
        {
            if (request.GetInfo().MediaType == MediaType.Playlist)
                await Queue.EnqueueAsync(request.SubRequests!);
            else
                await Queue.EnqueueAsync(request);
            mediaCallback(request.GetInfo(), MediaState.Queued, request.SubRequestInfo);
        }

        async Task<MediaRequest> QueueSubRequestsAsync(MediaRequest request)
        {
            await Queue.EnqueueAsync(request.SubRequests!);
            var first = await Queue.DequeueAsync();
            mediaCallback(request.GetInfo(), MediaState.Queued, request.SubRequestInfo);
            return first;
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
            if (downloading)
                return;

            if (Playing)
            {
                await QueueAsync(request);
                return;
            }

            MediaMetadata? requestInfo = null;
            PlayableMedia media;

            MediaRequest? subRequest = null;
            try
            {
                downloading = true;
                requestInfo = request.GetInfo();
                mediaCallback(requestInfo, MediaState.Downloading, request.SubRequestInfo);

                if (requestInfo.MediaType == MediaType.Playlist)
                {
                    subRequest = await QueueSubRequestsAsync(request);
                    mediaCallback(subRequest.GetInfo(), MediaState.Downloading, subRequest.SubRequestInfo);
                    media = await subRequest.GetMediaAsync();
                }
                else
                {
                    media = await request.GetMediaAsync();
                }

                Song = (media.Info, request.SubRequestInfo);
            }
            catch (Exception ex)
            {
                if (requestInfo != null)
                {
                    if (subRequest != null) // Subtract playlist duration by current media duration.
                        subRequest.SubRequestInfo!.Value.ParentRequestInfo.Duration -= requestInfo.Duration;

                    mediaCallback(requestInfo, MediaState.Error, request.SubRequestInfo);
                }

                if (ex is MediaUnavailableException)
                    throw ex;

                if (Queue.IsEmpty) throw;
                else
                {
                    var next = Shuffle ? await Queue.DequeueRandomAsync(Repeat) : await Queue.DequeueAsync(Repeat);
                    await PlayAsync(next, audioChannel);
                    return;
                }
            }
            finally { downloading = false; }

            await ConnectAsync(audioChannel);

            mediaCallback(media.Info, MediaState.Playing, subRequest?.SubRequestInfo ?? request.SubRequestInfo);
            using var audioProcessor = new FFmpegAudioProcessor(media.Info.DataInformation.MediaPath ?? throw new NullReferenceException("MediaPath was null."), media.Info.DataInformation.Format);

            try { await SendDataAsync(audioProcessor, audioChannel, GetChannelBitrate(audioChannel)); }
            catch (WebException) { } // Attempt to catch discord disconnects.

            if (Loop)
            {
                var next = request.GetInfo().MediaType == MediaType.Playlist ? subRequest ?? throw new CriticalException("Sub request was null.") : request;
                await PlayAsync(next, audioChannel);
            }
            mediaCallback(media.Info, MediaState.Finished, subRequest?.SubRequestInfo ?? request.SubRequestInfo);

            if (request.SubRequestInfo.HasValue) // Subtract playlist duration by current media duration.
                request.SubRequestInfo.Value.ParentRequestInfo.Duration -= media.Info.Duration;
            else if (subRequest != null)
                subRequest.SubRequestInfo!.Value.ParentRequestInfo.Duration -= media.Info.Duration;

            if (!Queue.IsEmpty)
            {
                var next = Shuffle ? await Queue.DequeueRandomAsync(Repeat) : await Queue.DequeueAsync(Repeat);
                await PlayAsync(next, audioChannel).ConfigureAwait(false);
                return;
            }

            await DisconnectAsync().ConfigureAwait(false);
        }
    }
}
