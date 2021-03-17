using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

using Discord;
using Discord.Audio;
using Discord.WebSocket;

using Melodica.Core.Exceptions;
using Melodica.Services.Audio;
using Melodica.Services.Downloaders.Exceptions;
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

        public delegate void MediaCallback(MediaInfo? info, MediaState state, MediaInfo? playlistInfo);

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

        public bool Loop { get => queue.Loop; set => queue.Loop = value; }

        public bool Shuffle { get => queue.Shuffle; set => queue.Shuffle = value; }

        public bool Repeat { get => queue.Repeat; set => queue.Repeat = value; }

        public TimeSpan Elapsed => new(durationTimer.Elapsed.Hours, durationTimer.Elapsed.Minutes, durationTimer.Elapsed.Seconds);

        private readonly MediaCallback mediaCallback;
        private readonly PlaybackStopwatch durationTimer = new();
        private readonly MediaQueue queue = new();
        private readonly SemaphoreSlim playLock = new(1);
        private CancellationTokenSource? cancellation;

        private IAudioClient? audioClient;
        private IAudioChannel? audioChannel;

        private bool skipRequested = false;
        private bool downloading = false;

        private PlayableMedia? currentSong;

        public MediaQueue GetQueue() => queue;

        public PlayableMedia? GetSong() => currentSong;

        static async ValueTask<bool> CheckIfAloneAsync(IAudioChannel channel)
        {
            var users = await channel.GetUsersAsync().FirstAsync();
            if (!users.IsOverSize(1))
                return true;
            else return false;
        }

        static int GetChannelBitrate(IAudioChannel channel)
        {
            return channel switch
            {
                SocketVoiceChannel sv => sv.Bitrate,
                _ => throw new CriticalException("Unable to get channel bitrate.")
            };
        }

        void WriteData(AudioProcessor audio, int bitrate, CancellationToken token)
        {
            bool BreakConditions() => token.IsCancellationRequested || skipRequested;

            void Pause()
            {
                durationTimer.Stop();
                while (Paused && !BreakConditions())
                {
                    Thread.Sleep(1000);
                }
                durationTimer.Start();
            }

            //TODO: Investigate why the bot is speaking but silent when looping.
            int count = 0;
            Span<byte> buffer = stackalloc byte[1024];
            using var input = audio.GetOutput();
            using var output = audioClient!.CreatePCMStream(AudioApplication.Music, bitrate, 1000, 0);
            durationTimer.Start();
            while ((count = input!.Read(buffer)) != 0)
            {
                if (Paused)
                {
                    Pause();
                }

                if (BreakConditions())
                    break;

                output.Write(buffer.Slice(0, count));
            }
            output.Flush();
        }

        private Task<bool> SendDataAsync(AudioProcessor audio, IAudioChannel channel, int bitrate, CancellationToken token)
        {
            bool abort = false;

            var writeThread = new Thread(() =>
            {
                try
                {
                    using var aloneTimer = new Timer(async _ =>
                    {
                        if (await CheckIfAloneAsync(channel))
                            await StopAsync();
                    }, null, 0, 5000);

                    WriteData(audio, bitrate, token);
                }
                catch (Exception)
                {
                    abort = true;
                }
                finally
                {
                    durationTimer.Reset();
                }
            });

            writeThread.IsBackground = false;
            writeThread.Priority = ThreadPriority.Highest;

            writeThread.Start();
            Playing = true;

            writeThread.Join(); // Thread exit
            Playing = false;

            if (skipRequested)
                skipRequested = false;

            return Task.FromResult(abort); // Returns true if an error occured.
        }

        async ValueTask DisconnectAsync()
        {
            if (audioChannel is not null)
            {
                await audioChannel.DisconnectAsync();
                audioChannel = null;
            }
            if (audioClient is not null)
            {
                await audioClient.StopAsync();
                audioClient.Dispose();
                audioClient = null;
            }
        }

        public async ValueTask StopAsync()
        {
            if (cancellation is null || (cancellation is not null && cancellation.IsCancellationRequested))
                return;

            await ClearAsync();
            cancellation!.Cancel();
        }

        public ValueTask SkipAsync()
        {
            skipRequested = true;
            return ValueTask.CompletedTask;
        }

        public ValueTask ClearAsync()
        {
            return queue.ClearAsync();
        }

        private async ValueTask ConnectAsync(IAudioChannel audioChannel)
        {
            if (audioClient is not null && (audioClient.ConnectionState == ConnectionState.Connected || audioClient.ConnectionState == ConnectionState.Connecting))
                return;

            audioClient = await audioChannel.ConnectAsync();
            this.audioChannel = audioChannel;
        }

        async Task PlayNextAsync(IAudioChannel channel, CancellationToken token, TimeSpan? startingPoint = null)
        {
            //TODO: Make not break when two songs are requested at the same time.
            var bitrate = GetChannelBitrate(channel);
            using var audio = new FFmpegAudioProcessor();
            var media = await queue.DequeueAsync();

            var collectionInfo = media.CollectionInfo;
            currentSong = media;

            var info = media.Info;

            if (!Loop)
                mediaCallback(info, MediaState.Downloading, null);

            await audio.StartProcess(media, startingPoint);

            if (!Loop)
                mediaCallback(info, MediaState.Playing, collectionInfo);

            bool faulted = await SendDataAsync(audio, channel, bitrate, token);
            if (faulted)
            {
                throw new CriticalException("SendDataAsync encountered a fatal error. (dbg-msg)");
            }

            if (!Loop)
                mediaCallback(info, MediaState.Finished, collectionInfo);

            if ((!Loop && queue.IsEmpty) || cancellation!.IsCancellationRequested)
            {
                await DisconnectAsync();
                return;
            }

            await PlayNextAsync(channel, token, null);
        }

        public async Task PlayAsync(IMediaRequest request, IAudioChannel channel, TimeSpan? startingPoint = null)
        {
            MediaCollection? collection = null;
            MediaInfo? collectionInfo = null;
            try
            {
                if (downloading)
                    return;

                downloading = true;
                await ConnectAsync(channel);

                var reqInfo = await request.GetInfoAsync();
                mediaCallback(reqInfo, MediaState.Downloading, null);
                collection = await request.GetMediaAsync();
                collectionInfo = collection.CollectionInfo;

                await queue.EnqueueAsync(collection);
                if (Playing)
                {
                    mediaCallback(collectionInfo, MediaState.Queued, null);
                    return;
                }

                await playLock.WaitAsync();
                Playing = true;
                downloading = false;

                if (reqInfo.MediaType == MediaType.Playlist)
                {
                    mediaCallback(reqInfo, MediaState.Queued, null);
                }
                cancellation = new();
                var token = cancellation.Token;
                await PlayNextAsync(channel, token, startingPoint);
            }
            catch (Exception ex)
            {
                mediaCallback(currentSong?.Info ?? collectionInfo, MediaState.Error, collectionInfo);
                await StopAsync();
                await DisconnectAsync();
                if (ex is not MediaUnavailableException)
                    throw;
            }
            finally
            {
                Playing = false;
                downloading = false;
                playLock.Release();
            }
        }

        public async Task SwitchAsync(IMediaRequest request, IAudioChannel channel)
        {
            await StopAsync();
            try
            {
                await playLock.WaitAsync();
                var media = await request.GetMediaAsync();
                await queue.PutFirstAsync(media);
                var colInfo = await request.GetInfoAsync();
                cancellation = new();
                var token = cancellation.Token;
                await PlayNextAsync(channel, token);
            }
            finally
            {
                playLock.Release();
            }
        }

        public async Task SetNextAsync(IMediaRequest request)
        {
            var media = await request.GetMediaAsync();
            await queue.PutFirstAsync(media);
        }
    }
}