﻿using Discord;
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
using System.Runtime.CompilerServices;

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
            this.queue = queue ?? new SongQueue();
        }

        public int Bitrate { get; set; } = 96 * 1024;
        private readonly bool alwaysUseMaxBitrate = true;

        public int BufferSize { get; set; } = 1024 / 2;        

        public bool Playing { get; private set; }

        public bool Shuffle { get; private set; }

        public bool Loop { get; private set; }

        public bool Paused { get; set; }

        private bool skip = false;

        private volatile bool switching = false;

        private AudioOutStream? discordOut;

        private CancellationTokenSource? playbackToken;

        private Thread? playbackThread;

        private readonly SongQueue queue;

        private IAudioChannel? channel;

        private IAudioClient? audioClient;

        private MediaRequest? currentRequest;

        private readonly SemaphoreSlim writeLock = new SemaphoreSlim(1);

        public MediaMetadata? GetSong() => currentRequest?.GetInfo();

        public string GetChannelName() => channel!.Name;

        public bool IsInChannel() => GetChannelName() != null;

        public void Skip() => skip = true;

        public SongQueue GetQueue() => queue;

        public Task ClearQueueAsync() => queue.ClearAsync();

        public MediaMetadata RemoveFromQueue(int index) => queue.RemoveAtAsync(index).Result.GetInfo();

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
                    else throw new CriticalException(null, e);
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
            currentRequest = null;
            await channel!.DisconnectAsync();
            await discordOut!.DisposeAsync();
            await audioClient!.StopAsync();
            await Task.Run(audioClient.Dispose);
            GC.Collect();
        }

        public Task ToggleLoopAsync(Action<(MediaMetadata info, bool wasLooping)>? callback = null)
        {
            if (currentRequest == null)
                throw new Exception("No song is playing.");

            if (GetSong()!.DataInformation.Format == "hls")
                throw new Exception("Can't loop a livestream.");

            callback?.Invoke((GetSong()!, Loop));
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
        public async Task PlayAsync(MediaRequest request, IAudioChannel channel, bool switchSong = false, bool loop = false, Action<(MediaMetadata info, SubRequestInfo subInfo, MediaState state)>? callback = null)
        {
            switching = switchSong;

            await ConnectAsync(channel);
            bool wasPlaying = Playing;
            bool wasShuffling = Shuffle;

            var requestType = request.RequestMediaType;
            var subRequests = await request.GetSubRequestsAsync();

            bool error = false;

            if (Playing && !switching)
            {
                switch (requestType)
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

            switch (requestType)
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

            await writeLock.WaitAsync();
            currentRequest = await queue.DequeueAsync();
            PlayableMedia? song = null;
            try
            {
                Playing = true; // Set playing to true so nobody is able to switch while a song is downloading.
                
                if (request.RequestMediaType != MediaType.Livestream && !Loop)
                    callback?.Invoke((currentRequest.GetInfo(), currentRequest.SubRequestInfo, MediaState.Downloading));

                song = await currentRequest.GetMediaAsync();
            }
            catch (Exception ex)
            {
                writeLock.Release();
                Playing = wasPlaying;
                Shuffle = wasShuffling;              

                callback?.Invoke((currentRequest.GetInfo(), currentRequest.SubRequestInfo, MediaState.Error));
                currentRequest = null;

                if (ex is CriticalException || queue.IsEmpty)
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

            if (!error && !Loop || (Loop && switching))
                callback?.Invoke((GetSong()!, currentRequest!.SubRequestInfo, MediaState.Playing));

            Loop = loop && !skip && currentRequest!.RequestMediaType != MediaType.Livestream;
            switching = false;
            skip = false;

            if (!error)
            {              
                playbackThread = BeginWrite(new AudioProcessor(song!.Info.DataInformation.MediaPath, BufferSize, song!.Info.DataInformation.Format));
                playbackThread.Start();
                playbackThread.Join();
            }

            if (!error && !Loop)
                callback?.Invoke((song!.Info, currentRequest!.SubRequestInfo, MediaState.Finished));

            writeLock.Release();

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

            if (queue.IsEmpty && !Loop)
            {
                Loop = false;
                Shuffle = false;
                await DismissAsync().ConfigureAwait(false);
                return;
            }
            var nextSong = Loop ? currentRequest! :
                           Shuffle ? await queue.DequeueRandomAsync() : await queue.DequeueAsync();
            await PlayAsync(nextSong, channel, false, Loop, callback!);
        }
    }
}
