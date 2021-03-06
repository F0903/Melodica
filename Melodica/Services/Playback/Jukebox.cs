﻿using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Discord;
using Discord.Audio;
using Discord.WebSocket;

using Melodica.Core.Exceptions;
using Melodica.Services.Audio;
using Melodica.Services.Media;
using Melodica.Services.Playback.Exceptions;
using Melodica.Services.Playback.Requests;
using Melodica.Utility.Extensions;

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

        public delegate void MediaCallback(MediaInfo info, MediaState state, MediaInfo? parentRequestInfo);

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

        public (MediaInfo info, MediaInfo? parentInfo)? Song { get; private set; }

        public TimeSpan Duration => new TimeSpan(durationTimer.Elapsed.Hours, durationTimer.Elapsed.Minutes, durationTimer.Elapsed.Seconds);

        private IAudioClient? audioClient;
        private readonly MediaCallback mediaCallback;
        private readonly PlaybackStopwatch durationTimer = new PlaybackStopwatch();

        private bool stopRequested = false;
        private bool downloading = false;

        private readonly SemaphoreSlim writeLock = new SemaphoreSlim(1);

        private static async Task<bool> CheckIfAloneAsync(IAudioChannel channel)
        {
            var users = await channel.GetUsersAsync().FirstAsync();
            if (!users.IsOverSize(1))
                return true;
            else return false;
        }

        private Task<bool> SendDataAsync(AudioProcessor audioProcessor, IAudioChannel channel, int bitrate)
        {
            bool abort = false;
            bool isAlone = false;
            bool BreakConditions() => stopRequested || isAlone;

            var writeThread = new Thread(() =>
            {
                using var aloneTimer = new Timer(x => isAlone = CheckIfAloneAsync(channel).Result, null, 0, 5000);

                int count = 0;
                Span<byte> buffer = stackalloc byte[1024];
                try
                {
                    using var input = audioProcessor.GetOutput();
                    using var output = audioClient!.CreatePCMStream(AudioApplication.Music, bitrate, 100, 0);
                    durationTimer.Start();
                    writeLock.Wait();
                    while ((count = input!.Read(buffer)) != 0)
                    {
                        if (Paused)
                        {
                            durationTimer.Stop();
                            while (Paused && !BreakConditions()) { Thread.Sleep(1000); }
                            durationTimer.Start();
                        }

                        output.Write(buffer.Slice(0, count));

                        if (BreakConditions()) break;
                    }
                    output.Flush();
                }
                catch (Exception ex) when (!(ex is TaskCanceledException)) { abort = true; }
                finally
                {
                    durationTimer.Reset();
                    writeLock.Release();
                }
            });

            writeThread.Name = "AudioWriteThread";
            writeThread.IsBackground = false;
            writeThread.Priority = ThreadPriority.Highest;

            writeThread.Start();
            Playing = true;

            writeThread.Join(); // Thread exit
            Playing = false;

            if (isAlone)
            {
                DisconnectAsync().ConfigureAwait(false);
                throw new EmptyChannelException();
            }

            if (stopRequested)
            {
                stopRequested = false;
                return Task.FromResult(false);
            }

            return Task.FromResult(abort); // Returns true if an error occured.
        }

        public async Task DisconnectAsync()
        {
            Song = null;
            await Queue.ClearAsync();

            await StopAsync();

            if (audioClient == null)
                return;
            await audioClient.StopAsync();
            audioClient = null;
        }

        private async Task StopAsync()
        {
            if (!Playing)
                return;
            Loop = false;
            Shuffle = false;
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
            Loop = false;
            stopRequested = true;
            return Task.CompletedTask;
        }

        public async Task SwitchAsync(MediaRequest request)
        {
            if (!Playing)
                throw new Exception("No song is playing.");
            await Queue.PutFirstAsync(request);
            Paused = false;
            Shuffle = false;
            Loop = false;
            stopRequested = true;
        }

        private async Task QueueAsync(MediaRequest request)
        {
            if (request.GetInfo().MediaType == MediaType.Playlist)
                await Queue.EnqueueAsync(request.SubRequests!);
            else
                await Queue.EnqueueAsync(request);
            mediaCallback(request.GetInfo(), MediaState.Queued, request.ParentRequestInfo);
        }

        private async Task<MediaRequest> QueueSubRequestsAsync(MediaRequest request)
        {
            await Queue.EnqueueAsync(request.SubRequests!);
            var first = await Queue.DequeueAsync();
            mediaCallback(request.GetInfo(), MediaState.Queued, request.ParentRequestInfo);
            return first;
        }

        private static int GetChannelBitrate(IAudioChannel channel)
        {
            return channel switch
            {
                SocketVoiceChannel sv => sv.Bitrate,
                _ => throw new CriticalException("Unable to get channel bitrate.")
            };
        }

        public async Task PlayAsync(MediaRequest request, IAudioChannel audioChannel, TimeSpan? startingPoint = null)
        {
            if (downloading)
                return;

            if (Playing)
            {
                await QueueAsync(request);
                return;
            }

            MediaInfo? requestInfo = null;
            PlayableMedia media;

            MediaRequest? subRequest = null;
            try
            {
                downloading = true;

                await ConnectAsync(audioChannel);

                requestInfo = request.GetInfo();
                if (!Loop) mediaCallback(requestInfo, MediaState.Downloading, request.ParentRequestInfo);

                if (requestInfo.MediaType == MediaType.Playlist)
                {
                    subRequest = await QueueSubRequestsAsync(request);
                    mediaCallback(subRequest.GetInfo(), MediaState.Downloading, subRequest.ParentRequestInfo);
                    media = await subRequest.GetMediaAsync();
                }
                else
                {
                    media = await request.GetMediaAsync();
                }

                Song = (media.Info, request.ParentRequestInfo);
            }
            catch
            {
                if (requestInfo != null)
                {
                    if (subRequest != null && subRequest.ParentRequestInfo != null)
                        subRequest.ParentRequestInfo.Duration -= requestInfo.Duration; // Subtract playlist duration by current media duration.

                    mediaCallback(requestInfo, MediaState.Error, request.ParentRequestInfo);
                }

                if (Queue.IsEmpty)
                {
                    await DisconnectAsync();
                    throw;
                }
                else
                {
                    downloading = false;
                    await PlayNext().ConfigureAwait(false);
                    return;
                }
            }
            finally { downloading = false; }

            async Task PlayNext()
            {
                var next = Shuffle ? await Queue.DequeueRandomAsync(Repeat) : await Queue.DequeueAsync(Repeat);
                await PlayAsync(next, audioChannel).ConfigureAwait(false);
            }

            async Task PlaySame(bool startFromLastPoint = false)
            {
                var next = request.GetInfo().MediaType == MediaType.Playlist ? subRequest ?? throw new CriticalException("Sub request was null.") : request;
                await PlayAsync(next, audioChannel, startFromLastPoint ? durationTimer.LastDuration : TimeSpan.Zero);
            }

            if (!Loop) mediaCallback(media.Info, MediaState.Playing, subRequest?.ParentRequestInfo ?? request.ParentRequestInfo);
            var audioProcessor = new FFmpegAudioProcessor();
            await audioProcessor.Process(media.Info.DataInformation.MediaPath ?? throw new NullReferenceException("MediaPath was null."), media.Info.DataInformation.Format, startingPoint);

            //TODO: Keep investigating the discord null ref events.
            var faulted = await SendDataAsync(audioProcessor, audioChannel, GetChannelBitrate(audioChannel));
            if (faulted) durationTimer.Elapsed = durationTimer.LastDuration;
            if (faulted || Loop)
            {
                await PlaySame(faulted).ConfigureAwait(false);
                return;
            }

            mediaCallback(media.Info, MediaState.Finished, subRequest?.ParentRequestInfo ?? request.ParentRequestInfo);

            if (request.ParentRequestInfo != null) // Subtract playlist duration by current media duration.
                request.ParentRequestInfo.Duration -= media.Info.Duration;
            else if (subRequest != null)
                if (subRequest.ParentRequestInfo != null)
                    subRequest.ParentRequestInfo.Duration -= media.Info.Duration;

            if (!Queue.IsEmpty)
            {
                await PlayNext().ConfigureAwait(false);
                return;
            }

            await DisconnectAsync().ConfigureAwait(false);
        }
    }
}