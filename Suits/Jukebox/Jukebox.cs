using Discord;
using Discord.Audio;
using Suits.Jukebox.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Suits.Jukebox.Services.Cache;
using Suits.Jukebox.Services.Downloaders;
using Suits.Jukebox.Models.Requests;
using AngleSharp.Io;
using Discord.WebSocket;
using Suits.Jukebox.Models.Exceptions;
using System.Diagnostics;

namespace Suits.Jukebox
{
    public enum MediaState
    {
        Error,
        Downloading,
        Queued,
        Playing,
        Finished
    }

    public sealed class Jukebox
    {
        public Jukebox(SongQueue? queue = null)
        {
            this.songQueue = queue ?? new SongQueue();
        }

        public int Bitrate { get; set; } = 96 * 1024;
        private readonly bool alwaysUseMaxBitrate = true;

        public int BufferSize { get; set; } = 1024 / 2;

        public PlayableMedia? CurrentSong { get; private set; }

        public bool Playing { get; private set; }

        public bool Shuffle { get; private set; }

        public bool Loop { get; private set; }

        public bool Paused { get; set; }

        private bool skip = false;

        private volatile bool switching = false;

        private AudioOutStream? discordOut;

        private CancellationTokenSource? playbackToken;

        private Thread? playbackThread;

        private readonly SongQueue songQueue;

        private IAudioChannel? channel;

        private IAudioClient? audioClient;

        public string GetChannelName() => channel!.Name;

        public bool IsInChannel() => GetChannelName() != null;

        public void Skip() => skip = true;

        public SongQueue GetQueue() => songQueue;

        public Task ClearQueueAsync() => songQueue.ClearAsync();

        public MediaMetadata RemoveFromQueue(int index) => songQueue.RemoveAtAsync(index).Result.GetInfo();

        private bool IsAlone() => false; // The old method currently throws a FileNotFoundException. Therefore this will first be reimplemented when Discord.Net gets updated on NuGet.

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
        private Thread BeginWrite(AudioProcessor audio)
        {
            if (audioClient == null)
                throw new NullReferenceException("Audio Client was null.");

            playbackToken = new CancellationTokenSource();

            bool BreakConditions() => IsAlone() || skip || switching || playbackToken!.IsCancellationRequested;

            void Write() // Refactor
            {
                var inS = audio.GetOutput();
                byte[] buffer = new byte[BufferSize];
                int bytesRead;
                try
                {
                    Playing = true;
                    while ((bytesRead = inS!.Read(buffer, 0, buffer.Length)) != 0)
                    {
                        var shouldBreak = BreakConditions();

                        while (Paused && !shouldBreak)
                        {
                            Thread.Sleep(1000);
                        }

                        if (shouldBreak)
                            break;

                        discordOut!.Write(buffer, 0, bytesRead);
                    }
                }
                catch (Exception e)
                {
                    // If the playback was stopped, simply ignore the exception that gets thrown.
                    if (e is OperationCanceledException)
                        return;

                    discordOut = CreateOutputStream();
                    Write();
                }
                finally
                {
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
            CurrentSong = null;
            await channel!.DisconnectAsync();
            await discordOut!.DisposeAsync();
            await audioClient!.StopAsync();
            await Task.Run(audioClient.Dispose);
        }

        public Task ToggleLoopAsync(Action<(MediaMetadata info, bool wasLooping)>? callback = null)
        {
            if (CurrentSong == null)
                throw new Exception("No song is playing.");

            if (CurrentSong!.Info.DataInformation.Format == "hls")
                throw new Exception("Can't loop a livestream.");

            callback?.Invoke((CurrentSong!.Info, Loop));
            Loop = !Loop;
            return Task.CompletedTask;
        }

        public Task ToggleShuffleAsync(Action<MediaMetadata, bool>? callback = null)
        {
            callback?.Invoke(GetQueue().GetMediaInfo(), Shuffle);
            Shuffle = !Shuffle;
            return Task.CompletedTask;
        }

        public Task StopAsync(bool clearQueue = true)
        {
            Loop = false;
            Paused = false;
            if (clearQueue)
                songQueue.ClearAsync();
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
        public async Task PlayAsync(MediaRequest request, IAudioChannel channel, bool switchSong = false, bool loop = false, Action<(MediaMetadata info, MediaState state)>? callback = null)
        {
            switching = switchSong;

            await ConnectAsync(channel);
            bool wasPlaying = Playing;
            bool wasShuffling = Shuffle;

            var requestType = request.MediaType;
            var subRequests = await request.GetSubRequestsAsync();

            bool error = false;

            if (Playing && !switching)
            {
                switch (requestType)
                {
                    case MediaType.Video:
                        await songQueue.EnqueueAsync(request);
                        break;
                    case MediaType.Playlist:
                        await songQueue.EnqueueAsync(subRequests);
                        break;
                    case MediaType.Livestream:
                        goto case MediaType.Video;
                    default:
                        throw new CriticalException("Unknown error happened in RequestType switch.");
                }

                callback?.Invoke((request.GetInfo(), MediaState.Queued));
                return;
            }

            switch (requestType)
            {
                case MediaType.Video:
                    await songQueue.PutFirst(request);
                    break;
                case MediaType.Playlist:
                    await songQueue.PutFirst(subRequests);
                    break;
                case MediaType.Livestream:
                    goto case MediaType.Video;
                default:
                    throw new CriticalException("Unknown error happened in RequestType switch.");
            }

            try
            {
                Playing = true; // Set playing to true so nobody is able to switch while a song is downloading.
                if (request.MediaType != MediaType.Livestream && !Loop)
                    callback?.Invoke((request.GetInfo(), MediaState.Downloading));

                CurrentSong = await (await songQueue.DequeueAsync()).GetMediaAsync();
            }
            catch (Exception ex)
            {
                Playing = wasPlaying;
                Shuffle = wasShuffling;
                CurrentSong = null;

                callback?.Invoke((request.GetInfo(), MediaState.Error));

                if (ex is CriticalException)
                {
                    await DismissAsync();
                    throw ex;
                }
                error = true;
            }
            finally
            {
                GC.Collect();
            }

            if (!error && !Loop && request.MediaType == MediaType.Playlist)
                callback?.Invoke((request.GetInfo(), MediaState.Playing));

            if (!error && !Loop || (Loop && switching))
                callback?.Invoke((CurrentSong!.Info, MediaState.Playing));

            Loop = loop && !skip && request.MediaType != MediaType.Livestream;
            switching = false;
            skip = false;

            if (!error)
            {
                playbackThread = BeginWrite(new AudioProcessor(CurrentSong!.Info.DataInformation.MediaPath, BufferSize, CurrentSong.Info.DataInformation.Format));
                playbackThread.Start();
                playbackThread.Join();
            }

            if (!error && !Loop)
                callback?.Invoke((CurrentSong!.Info, MediaState.Finished));

            if (switching)
            {
                switching = false;
                return;
            }

            if (IsAlone())
            {
                await DismissAsync().ConfigureAwait(false);
                return;
            }

            if (songQueue.IsEmpty && !Loop)
            {
                Loop = false;
                Shuffle = false;
                await DismissAsync().ConfigureAwait(false);
                return;
            }

            var nextSong = Loop ? new MediaRequest(CurrentSong!) :
                           Shuffle ? await songQueue.DequeueRandomAsync() : await songQueue.DequeueAsync();
            await PlayAsync(nextSong, channel, false, Loop, callback!);
        }
    }
}
