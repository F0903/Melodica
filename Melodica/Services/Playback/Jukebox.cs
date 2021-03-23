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
        public Jukebox(IMessageChannel mediaCallback)
        {
            this.embedHandler = new(mediaCallback);
        }

        public delegate ValueTask MediaCallback(MediaInfo info, MediaInfo? playlistInfo, MediaState state);

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

        public bool Playing => !playLock.IsSet;

        public bool Loop { get => queue.Loop; set => queue.Loop = value; }

        public bool Shuffle { get => queue.Shuffle; set => queue.Shuffle = value; }

        public bool Repeat { get => queue.Repeat; set => queue.Repeat = value; }

        public TimeSpan Elapsed => new(durationTimer.Elapsed.Hours, durationTimer.Elapsed.Minutes, durationTimer.Elapsed.Seconds);

        private readonly PlaybackEmbedHandler embedHandler;
        private readonly PlaybackStopwatch durationTimer = new();
        private readonly MediaQueue queue = new();
        private readonly ManualResetEventSlim playLock = new(true);
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

        static void SendSilence(AudioOutStream output, int samples = 256)
        {
            const int channels = 2;
            const int bits = 16;
            var bytes = (channels * bits) * samples;
            Span<byte> buffer = bytes < 1024 ? stackalloc byte[bytes] : new byte[bytes];
            output.Write(buffer);
        }

        void WriteData(AudioProcessor audio, AudioOutStream output, CancellationToken token)
        {
            bool BreakConditions() => token.IsCancellationRequested || skipRequested;

            void Pause()
            {
                durationTimer.Stop();
                SendSilence(output);
                while (Paused && !BreakConditions())
                {
                    Thread.Sleep(1000);
                }
                durationTimer.Start();
            }

            int count = 0;
            Span<byte> buffer = stackalloc byte[1024];
            var input = audio.GetOutput();
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
            SendSilence(output);
            output.Flush();
        }

        private Task<bool> SendDataAsync(AudioProcessor audio, AudioOutStream output, IAudioChannel channel, CancellationToken token)
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

                    WriteData(audio, output, token);
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
            writeThread.Join(); // Thread exit

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

        async Task PlayNextAsync(IAudioChannel channel, AudioOutStream output, CancellationToken token, TimeSpan? startingPoint = null)
        {
            using var audio = new FFmpegAudioProcessor();
            var media = await queue.DequeueAsync();
            if (media is null)
                throw new CriticalException("Song from queue was null.");

            currentSong = media;
            var info = media.Info;
            var collectionInfo = media.CollectionInfo;


            if (!Loop)
                await embedHandler.MediaCallback(info, collectionInfo, MediaState.Downloading);

            var dataInfo = await media.SaveDataAsync();
            await audio.StartProcess(dataInfo, startingPoint);

            if (!Loop)
                await embedHandler.MediaCallback(info, collectionInfo, MediaState.Playing);

            bool faulted = await SendDataAsync(audio, output, channel, token);
            if (faulted)
            {
                throw new CriticalException("SendDataAsync encountered a fatal error. (dbg-msg)");
            }

            if (!Loop)
                await embedHandler.MediaCallback(info, collectionInfo, MediaState.Finished);

            if ((!Loop && queue.IsEmpty) || cancellation!.IsCancellationRequested)
            {
                await DisconnectAsync();
                return;
            }

            await PlayNextAsync(channel, output, token, null);
        }

        public async Task PlayAsync(IMediaRequest request, IAudioChannel channel, TimeSpan? startingPoint = null)
        {
            MediaInfo? reqInfo = null;
            MediaInfo? colInfo = null;
            try
            {
                if (downloading)
                    return;

                downloading = true;
                await ConnectAsync(channel);

                reqInfo = await request.GetInfoAsync();
                var collection = await request.GetMediaAsync();
                colInfo = collection.CollectionInfo;

                await queue.EnqueueAsync(collection);
                if (Playing)
                {
                    await embedHandler.MediaCallback(reqInfo, colInfo, MediaState.Queued);
                    return;
                }

                playLock.Wait();
                playLock.Reset();
                downloading = false;

                if (reqInfo.MediaType == MediaType.Playlist)
                {
                    await embedHandler.MediaCallback(reqInfo, colInfo, MediaState.Queued);
                }
                cancellation = new();
                var token = cancellation.Token;

                var bitrate = GetChannelBitrate(channel);
                using var output = audioClient!.CreatePCMStream(AudioApplication.Music, bitrate, 1000, 0);

                await PlayNextAsync(channel, output, token, startingPoint);
            }
            catch (Exception ex)
            {
                downloading = false;
                if (reqInfo is not null)
                    await embedHandler.MediaCallback(reqInfo, colInfo, MediaState.Error);
                await StopAsync();
                await DisconnectAsync();
                if (ex is not MediaUnavailableException)
                    throw;
            }
            finally
            {
                playLock.Set();
            }
        }

        public async Task SwitchAsync(IMediaRequest request, IAudioChannel channel)
        {
            //TODO: Make work
            await StopAsync();
            await PlayAsync(request, channel);
        }

        public async Task SetNextAsync(IMediaRequest request)
        {
            var media = await request.GetMediaAsync();
            await queue.PutFirstAsync(media);
        }
    }
}