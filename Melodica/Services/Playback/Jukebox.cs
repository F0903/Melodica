using System;
using System.Linq;
using System.Net.Http;
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

        public delegate void MediaCallback(MediaInfo? info, MediaState state, MediaInfo? playlistInfo);

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

        public TimeSpan Elapsed => new(durationTimer.Elapsed.Hours, durationTimer.Elapsed.Minutes, durationTimer.Elapsed.Seconds);

        private readonly MediaCallback mediaCallback;
        private readonly PlaybackStopwatch durationTimer = new();
        private readonly SongQueue queue = new();
        private readonly SemaphoreSlim writeLock = new(1);
        private readonly SemaphoreSlim playLock = new(1);
        private CancellationTokenSource? cancellation;

        private IAudioClient? audioClient;
        private IAudioChannel? audioChannel;

        private bool skipRequested = false;

        private PlayableMedia? currentSong;
        private MediaInfo? currentPlaylist;

        public SongQueue GetQueue() => queue;

        public (MediaInfo song, MediaInfo? playlist)? GetSong()
        {
            if (currentSong is null)
                return null;
            return (currentSong.Info, currentPlaylist);
        }

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

        void WriteData(ref AudioProcessor audio, int bitrate, CancellationToken token)
        {
            bool BreakConditions() => token.IsCancellationRequested || skipRequested;

            int count = 0;
            Span<byte> buffer = stackalloc byte[1024];
            using var input = audio.GetOutput();
            using var output = audioClient!.CreatePCMStream(AudioApplication.Music, bitrate, 100, 0);
            durationTimer.Start();
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

        private Task<bool> SendDataAsync(AudioProcessor audio, IAudioChannel channel, int bitrate, CancellationToken token)
        {
            bool abort = false;

            var writeThread = new Thread(() =>
            {
                using var aloneTimer = new Timer(async _ =>
                {
                    if (await CheckIfAloneAsync(channel))
                        await StopAsync();
                }, null, 0, 5000);

                try
                {
                    writeLock.Wait(token);
                    WriteData(ref audio, bitrate, token);
                }
                catch (Exception)
                {
                    abort = true;
                }
                finally
                {
                    durationTimer.Reset();
                    writeLock.Release();
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

        public async ValueTask StopAsync(bool disconnect = true)
        {
            if (cancellation is null)
                return;
            cancellation.Cancel();
            Playing = false;
            if (audioChannel is not null && disconnect)
            {
                await audioChannel.DisconnectAsync();
                audioChannel = null;
                await ClearAsync();
            }
            if (audioClient is not null)
            {
                await audioClient.StopAsync();
                audioClient.Dispose();
                audioClient = null;
            }
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

        async Task PlaySameAsync(IAudioChannel channel, CancellationToken token)
        {
            if (currentSong is null)
                throw new NullReferenceException("CurrentSong was null. Cannot play same. (dbg-err)");

            var bitrate = GetChannelBitrate(channel);
            var audio = new FFmpegAudioProcessor();

            await audio.Process(currentSong);
            await SendDataAsync(audio, channel, bitrate, token);
        }

        async Task PlayNextAsync(MediaInfo? collectionInfo, IAudioChannel channel, CancellationToken token, TimeSpan? startingPoint = null)
        {
            var bitrate = GetChannelBitrate(channel);
            var audio = new FFmpegAudioProcessor();
            PlayableMedia media = Shuffle ? await queue.DequeueRandomAsync(Repeat) : await queue.DequeueAsync(Repeat);

            currentSong = media;
            currentPlaylist = collectionInfo;

            await audio.Process(media, startingPoint);
            if (collectionInfo is not null && collectionInfo.MediaType == MediaType.Playlist)
            {
                mediaCallback(media.Info, MediaState.Playing, collectionInfo);
            }
            else
            {
                mediaCallback(media.Info, MediaState.Playing, null);
            }

            if(await SendDataAsync(audio, channel, bitrate, token)) // Returns true on fatal error.
            {
                throw new CriticalException("SendDataAsync encountered a fatal error. (dbg-msg)");
            }

            if (Loop)
            {
                await PlaySameAsync(channel, token).ConfigureAwait(false);
                return;
            }

            mediaCallback(media.Info, MediaState.Finished, collectionInfo);

            if (queue.IsEmpty)
            {
                await StopAsync().ConfigureAwait(false);
                return;
            }

            await PlayNextAsync(collectionInfo, channel, token, null).ConfigureAwait(false);
        }

        public async Task PlayAsync(IMediaRequest request, IAudioChannel channel, TimeSpan? startingPoint = null)
        {
            try
            {
                await ConnectAsync(channel);

                var collection = await request.GetMediaAsync();

                await queue.EnqueueAsync(collection);
                var colInfo = collection.CollectionInfo;
                if (Playing)
                {
                    mediaCallback(colInfo, MediaState.Queued, null);
                    return;
                }

                await playLock.WaitAsync();
                Playing = true;
                var reqInfo = await request.GetInfoAsync();
                var state = reqInfo.MediaType == MediaType.Playlist ? MediaState.Queued : MediaState.Downloading;
                cancellation = new();
                var token = cancellation.Token;
                mediaCallback(reqInfo, state, null);
                await PlayNextAsync(colInfo, channel, token, startingPoint);
            }
            catch (Exception)
            {
                mediaCallback(currentSong?.Info, MediaState.Error, currentPlaylist);
                throw;
            }
            finally
            {
                playLock.Release();
            }
        }

        public async Task SwitchAsync(IMediaRequest request, IAudioChannel channel)
        {
            await StopAsync(false);
            var media = await request.GetMediaAsync();
            await queue.PutFirstAsync(media);
            var colInfo = await request.GetInfoAsync();
            cancellation = new();
            var token = cancellation.Token;
            await PlayNextAsync(colInfo, channel, token);
        }

        public async Task SetNextAsync(IMediaRequest request)
        {
            var media = await request.GetMediaAsync();
            await queue.PutFirstAsync(media);
        }

        public async Task ContinueFromQueue(IAudioChannel channel)
        {
            if (queue.IsEmpty)
                throw new Exception("Cannot continue from an empty queue.");
            cancellation = new();
            var token = cancellation.Token;
            await PlayNextAsync(null, channel, token).ConfigureAwait(false);
        }
    }
}