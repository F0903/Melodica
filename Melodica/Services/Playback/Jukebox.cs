using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Discord;
using Discord.Audio;
using Discord.WebSocket;

using Melodica.Core.Exceptions;
using Melodica.Services.Audio;
using Melodica.Services.Playback.Models;
using Melodica.Services.Playback.Models.Requests;

namespace Melodica.Services.Playback
{
    public enum MediaState
    {
        Error,
        Downloading,
        Queued,
        Playing,
        Finished
    }

    public class EmptyChannelException : Exception
    { }

    public sealed class Jukebox
    {
        public Jukebox(SongQueue? queue = null)
        {
            this.queue = queue ?? new SongQueue();
        }

        public int Bitrate { get; set; } = 96 * 1024;
        private readonly bool alwaysUseMaxBitrate = true;

        public int BufferSize { get; set; } = 2 * 1024;

        private bool skip = false;

        private volatile bool switching = false;

        private AudioOutStream? discordOut;

        private CancellationTokenSource? playbackToken;

        private Thread? playbackThread;

        private readonly SongQueue queue;

        private IAudioChannel? channel;

        private IAudioClient? audioClient;

        private MediaRequestBase? currentRequest;

        private readonly SemaphoreSlim writeLock = new SemaphoreSlim(1);

        private readonly Stopwatch durationTimer = new Stopwatch();

        public TimeSpan Duration => new TimeSpan(durationTimer.Elapsed.Hours, durationTimer.Elapsed.Minutes, durationTimer.Elapsed.Seconds);


        public (MediaMetadata info, SubRequestInfo? subInfo) GetSong() => ((currentRequest ?? throw new Exception("No song is playing")).GetInfo(), currentRequest.SubRequestInfo);

        public string GetChannelName() => channel!.Name;

        public void Skip() => skip = true;

        public SongQueue GetQueue() => queue;

        public Task ClearQueueAsync() => queue.ClearAsync();

        public MediaMetadata RemoveFromQueue(Index index) => queue.RemoveAtAsync(index.IsFromEnd ? queue.Length - index.Value - 1 : index.Value).Result.GetInfo();

        private bool IsAlone() => channel!.GetUsersAsync().FirstAsync().Result.Count < 2;


        public bool Playing { get; private set; }

        public bool Shuffle { get; set; }

        public bool Repeat { get; set; }

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


        private AudioOutStream CreateOutputStream()
        {
            int bitrate;
            if (channel is SocketVoiceChannel ch)
            {
                var chFullBitrate = ch.Bitrate / 1000 * 1024;
                bitrate = alwaysUseMaxBitrate || Bitrate > chFullBitrate ? chFullBitrate : Bitrate;
            }
            else bitrate = Bitrate;
            return audioClient!.CreatePCMStream(AudioApplication.Music, bitrate, 100, 0);
        }

        /// <summary>
        /// Begins the write using the specified audio processor.
        /// </summary>
        /// <param name="audio"> The audio processor to use. </param>
        /// <returns> The writing thread. </returns>
        private Thread BeginWrite(ExternalAudioProcessor audio)
        {
            if (audioClient == null)
                throw new NullReferenceException("Audio Client was null.");

            playbackToken = new CancellationTokenSource();

            bool BreakConditions() => skip || switching || playbackToken!.IsCancellationRequested;

            void Write()
            {
                var reqInfo = (currentRequest ?? throw new Exception("currentRequest was null in write loop. Please contact owner.")).GetInfo();
                var inS = audio.GetOutput();
                byte[] buffer = new byte[BufferSize];
                int bytesRead;
                try
                {
                    durationTimer.Start();
                    Playing = true;
                    while ((bytesRead = inS!.Read(buffer, 0, buffer.Length)) != 0)
                    {
                        var shouldBreak = BreakConditions();
                        if (IsAlone()) throw new EmptyChannelException();

                        while (Paused && !shouldBreak)
                        {
                            Thread.Sleep(1000);
                        }

                        if (shouldBreak)
                            break;

                        discordOut!.Write(buffer, 0, bytesRead);
                    }
                    durationTimer.Stop();
                }
                catch (Exception)
                {
                    // Just swallow all exceptions for now due to this being another thread, so any exceptions will crash the program.
                    // This could be a good candidate for use in some file logging.
                }
                finally
                {
                    durationTimer.Reset();
                    Playing = false;
                    audio.Dispose();
                    discordOut!.Flush();
                }
            }
            return new Thread(Write)
            {
                Name = "PlaybackThread",
                IsBackground = false,
                Priority = ThreadPriority.Highest
            };
        }

        private async Task DismissAsync()
        {
            currentRequest = null;
            await channel!.DisconnectAsync();
            await discordOut!.DisposeAsync();
            await audioClient!.StopAsync();
            await Task.Run(audioClient.Dispose);
            GC.Collect();
        }

        public Task StopAsync(bool clearQueue = true)
        {
            Loop = false;
            Paused = false;
            if (clearQueue)
                queue.ClearAsync();
            playbackToken?.Cancel();
            playbackThread?.Join();

            return Task.CompletedTask;
        }

        private async Task ConnectAsync(IAudioChannel channel)
        {
            bool badClient = audioClient == null ||
                             audioClient.ConnectionState == ConnectionState.Disconnected ||
                             audioClient.ConnectionState == ConnectionState.Disconnecting;
            audioClient = badClient ? await channel.ConnectAsync() : audioClient!;
            this.channel = channel;
            discordOut = badClient ? CreateOutputStream() : discordOut;
        }

        /// <summary>
        /// Plays the specified MediaRequest.
        /// </summary>
        /// <param name="request"> The Media to request playback. </param>
        /// <param name="channel"> The audio channel to output to. </param>
        /// <param name="switchSong"> Should the currently playing song be replaced with this? </param>
        /// <param name="loop"> Should this media be put on loop? </param>
        /// <param name="callbacks"> Callbacks for different states. </param>
        /// <returns></returns>
        public async Task PlayAsync(MediaRequestBase request, IAudioChannel channel, bool switchSong = false, bool loop = false, Action<(MediaMetadata info, SubRequestInfo? subInfo, MediaState state)>? callback = null)
        {
            //TODO: This whole thing might need a kind refactoring.

            //TODO: Fix media empty path bug (something may be going on in the cache) (ref song: https://open.spotify.com/track/0CpTNItafURRFujw9WAKfR?si=yRmzFCJsT6KlCVGCVcLhZQ)

            if (switching = switchSong)
                Paused = false;

            await ConnectAsync(channel);
            bool wasPlaying = Playing;
            bool wasShuffling = Shuffle;
            Loop = loop; // Remove this line if loop doesn't work. (it should)

            bool error = false;

            async Task Error(Exception ex)
            {
                Playing = wasPlaying;
                Shuffle = wasShuffling;

                if (currentRequest != null)
                    callback?.Invoke((currentRequest!.GetInfo(), currentRequest?.SubRequestInfo, MediaState.Error));
                currentRequest = null;

                if (ex is CriticalException || queue.IsEmpty)
                {
                    await DismissAsync();
                    Playing = false;
                    throw ex;
                }
                error = true; // Use this variable instead of returning so playback wont be stopped.
            }

            MediaMetadata? requestInfo = null;
            try { requestInfo = request.GetInfo(); }
            catch (Exception ex) { await Error(ex); }

            var subRequests = request.SubRequests!;

            if (Playing && !switching)
            {
                switch (requestInfo?.MediaType)
                {
                    case MediaType.Video:
                        await queue.EnqueueAsync(request);
                        break;
                    case MediaType.Playlist:
                        await queue.EnqueueAsync(subRequests);
                        break;
                    case MediaType.Livestream:
                        goto case MediaType.Video;
                    default:
                        throw new CriticalException("Unknown error happened in RequestType switch.");
                }

                callback?.Invoke((request.GetInfo(), request.SubRequestInfo, MediaState.Queued));
                return;
            }

            switch (requestInfo?.MediaType)
            {
                case MediaType.Video:
                    await queue.PutFirst(request);
                    break;
                case MediaType.Playlist:
                    await queue.PutFirst(subRequests);
                    break;
                case MediaType.Livestream:
                    goto case MediaType.Video;
                default:
                    throw new CriticalException("Unknown error happened in RequestType switch.");
            }

            bool IsRequestDownloadable() => !(request is LocalMediaRequest) && !(request is AttachmentMediaRequest) && requestInfo.MediaType != MediaType.Livestream;

            await writeLock.WaitAsync();
            currentRequest = await queue.DequeueAsync();
            PlayableMedia? song = null;
            try
            {
                Playing = true;

                if (IsRequestDownloadable() && !Loop)
                    callback?.Invoke((currentRequest.GetInfo(), currentRequest.SubRequestInfo, MediaState.Downloading));

                song = await currentRequest.GetMediaAsync();
            }
            catch (Exception ex)
            {
                await Error(ex);
            }
            finally
            {
                writeLock.Release();
                GC.Collect();
            }

            if (!error && !Loop || (Loop && switching))
            {
                var (info, subInfo) = GetSong();
                callback?.Invoke((info, subInfo, MediaState.Playing));
            }

            Loop = loop && !skip && currentRequest!.GetInfo().MediaType != MediaType.Livestream;
            switching = false;
            skip = false;

            if (!error)
            {
                try
                {
                    playbackThread = BeginWrite(new FFmpegAudioProcessor(song!.Info.DataInformation.MediaPath!, song!.Info.DataInformation.Format));
                    playbackThread.Start();
                    playbackThread.Join();
                }
                catch (Exception ex) { await Error(ex); }
                finally { writeLock.Release(); } // Just in case
            }

            if (!error && !Loop)
                callback?.Invoke((song!.Info, currentRequest!.SubRequestInfo, MediaState.Finished));

            // Subtract playlist total duration by the finished song.
            if(currentRequest != null && currentRequest!.SubRequestInfo.HasValue)
                currentRequest!.SubRequestInfo.Value.ParentRequestInfo.Duration -= currentRequest.GetInfo().Duration;

            if (switching)
            {
                switching = false;
                return;
            }

            if (IsAlone())
            {
                await DismissAsync().ConfigureAwait(false);
                throw new EmptyChannelException();
            }

            if (queue.IsEmpty && !Loop)
            {
                Loop = false;
                Shuffle = false;
                await DismissAsync().ConfigureAwait(false);
                return;
            }
            var nextSong = Loop ? currentRequest! :
                           Shuffle ? await queue.DequeueRandomAsync(Repeat) : await queue.DequeueAsync(Repeat);
            await PlayAsync(nextSong, channel, false, Loop, callback!);
        }
    }
}
