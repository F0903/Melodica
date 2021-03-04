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

        public SongQueue Queue { get; } = new SongQueue();

        public (MediaInfo info, MediaInfo? playlistInfo)? CurrentSong { get; private set; }

        public TimeSpan Elapsed => new TimeSpan(durationTimer.Elapsed.Hours, durationTimer.Elapsed.Minutes, durationTimer.Elapsed.Seconds);

        private readonly MediaCallback mediaCallback;
        private readonly PlaybackStopwatch durationTimer = new PlaybackStopwatch();
        private readonly SemaphoreSlim writeLock = new SemaphoreSlim(1);

        private IAudioClient? audioClient;
        private CancellationTokenSource? tokenSource;
        private bool busy = false;

        static async Task<bool> CheckIfAloneAsync(IAudioChannel channel)
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

        private Task<bool> SendDataAsync(AudioProcessor audioProcessor, IAudioChannel channel, int bitrate, CancellationToken cancellation)
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
                using var aloneTimer = new Timer(x => isAlone = CheckIfAloneAsync(channel).Result, null, 0, 5000);

                try
                {
                    WriteData();
                }
                catch (Exception ex) when (!(ex is TaskCanceledException))
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
                StopAsync().ConfigureAwait(false);
                throw new EmptyChannelException();
            }

            return Task.FromResult(abort); // Returns true if an error occured.
        }

        public Task StopAsync()
        {
            tokenSource?.Cancel();
            audioClient?.Dispose();
            return Task.CompletedTask;
        }

        private async Task ConnectAsync(IAudioChannel audioChannel)
        {
            if (audioClient is not null && (audioClient.ConnectionState == ConnectionState.Connected || audioClient.ConnectionState == ConnectionState.Connecting))
                return;

            audioClient = await audioChannel.ConnectAsync();
        }

        async Task PlayMediaAsync(PlayableMedia media, IAudioChannel channel, TimeSpan? startingPoint = null)
        {
            tokenSource = new CancellationTokenSource();
            var token = tokenSource.Token;
            var bitrate = GetChannelBitrate(channel);
            var audio = new FFmpegAudioProcessor();

            await audio.Process(media, startingPoint);
            await SendDataAsync(audio, channel, bitrate, token);
        }

        public async Task PlayAsync(IMediaRequest request, IAudioChannel channel, TimeSpan? startingPoint = null)
        {
            await ConnectAsync(channel);

            mediaCallback(await request.GetInfoAsync(), MediaState.Downloading, null);
            var collection = await request.GetMediaAsync();

            try
            {
                await Queue.EnqueueAsync(collection);
                mediaCallback(collection.CollectionInfo, MediaState.Queued, null);

                if (Playing)
                    return;

                Playing = true;
                var media = Shuffle ? await Queue.DequeueRandomAsync(Repeat) : await Queue.DequeueAsync(Repeat);

                var colMediaType = collection.CollectionInfo.MediaType;
                if (colMediaType == MediaType.Playlist)
                {
                    mediaCallback(media.Info, MediaState.Playing, collection.CollectionInfo);
                }
                else
                {
                    mediaCallback(media.Info, MediaState.Playing, null);
                }

                await PlayMediaAsync(media, channel, startingPoint);
                Playing = false;
            }
            catch (TaskCanceledException)
            { }
        }
    }
}