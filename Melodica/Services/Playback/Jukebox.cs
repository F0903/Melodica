using System;
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

        public delegate void MediaCallback(MediaInfo info, MediaState state, MediaInfo? playlistInfo);

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
        private readonly SemaphoreSlim writeLock = new(1);
        private readonly SongQueue queue = new();

        private IAudioClient? audioClient;
        private IAudioChannel? audioChannel;
        private CancellationTokenSource? tokenSource;
        private bool skipRequested = false; //TODO:

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

        private async Task<bool> SendDataAsync(AudioProcessor audioProcessor, IAudioChannel channel, int bitrate, CancellationToken cancellation)
        {
            bool abort = false;
            bool isAlone = false;

            bool BreakConditions() => cancellation.IsCancellationRequested || isAlone;

            void WriteData()
            {
                int count = 0;
                Span<byte> buffer = stackalloc byte[1024];
                using var input = audioProcessor.GetOutput();
                using var output = audioClient!.CreatePCMStream(AudioApplication.Music, bitrate, 100, 0);
                durationTimer.Start();
                writeLock.Wait(cancellation);
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

            var writeThread = new Thread(() =>
            {
                using var aloneTimer = new Timer(async x => isAlone = await CheckIfAloneAsync(channel), null, 0, 5000);

                try
                {
                    WriteData();
                }
                catch (Exception ex) when (ex is not TaskCanceledException)
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

            if (isAlone)
            {
                await StopAsync().ConfigureAwait(false);
                throw new EmptyChannelException();
            }

            return abort; // Returns true if an error occured.
        }

        public async ValueTask StopAsync(bool disconnect = true)
        {
            tokenSource?.Cancel();
            if (audioChannel is not null && disconnect)
                await audioChannel.DisconnectAsync();
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
        }

        async Task PlaySameAsync(IAudioChannel channel)
        {
            if (currentSong is null)
                throw new NullReferenceException("CurrentSong was null. Cannot play same. (dbg-err)");

            tokenSource = new CancellationTokenSource();
            var token = tokenSource.Token;
            var bitrate = GetChannelBitrate(channel);
            var audio = new FFmpegAudioProcessor();

            await audio.Process(currentSong);
            await SendDataAsync(audio, channel, bitrate, token);
        }

        async Task PlayNextAsync(MediaInfo? collectionInfo, IAudioChannel channel, TimeSpan? startingPoint = null)
        {
            tokenSource = new CancellationTokenSource();
            var token = tokenSource.Token;
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
            await SendDataAsync(audio, channel, bitrate, token);

            if (Loop)
            {
                await PlaySameAsync(channel).ConfigureAwait(false);
                return;
            }

            mediaCallback(media.Info, MediaState.Finished, collectionInfo);

            if (queue.IsEmpty)
            {
                await StopAsync().ConfigureAwait(false);
                return;
            }

            await PlayNextAsync(collectionInfo, channel, null).ConfigureAwait(false);
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

                Playing = true;
                var reqInfo = await request.GetInfoAsync();
                var state = reqInfo.MediaType == MediaType.Playlist ? MediaState.Queued : MediaState.Downloading;
                mediaCallback(reqInfo, state, null);
                await PlayNextAsync(colInfo, channel, startingPoint);
                Playing = false;
            }
            catch (TaskCanceledException)
            {
                mediaCallback(currentSong!.Info, MediaState.Finished, currentPlaylist);
            }
            catch (Exception)
            {
                await StopAsync();
                mediaCallback(currentSong!.Info, MediaState.Error, currentPlaylist);
                throw;
            }
            finally
            {
                Playing = false;
            }
        }

        public async Task SwitchAsync(IMediaRequest request, IAudioChannel channel)
        {
            await StopAsync();
            var media = await request.GetMediaAsync();
            await queue.PutFirstAsync(media);
            var colInfo = await request.GetInfoAsync();
            await PlayNextAsync(colInfo, channel);
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
            await PlayNextAsync(null, channel).ConfigureAwait(false);
        }
    }
}