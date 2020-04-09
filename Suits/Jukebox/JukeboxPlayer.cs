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

namespace Suits.Jukebox
{
    public sealed class JukeboxPlayer
    {
        public JukeboxPlayer(SongQueue? queue = null, int bitrate = DefaultBitrate, int bufferSize = DefaultBufferSize)
        {
            this.songQueue = queue ?? new SongQueue();
            Bitrate = bitrate;
            BufferSize = bufferSize;
        }

        public const int DefaultBitrate = 128 * 1024;
        public const int DefaultBufferSize = 1024 / 2;

        public int Bitrate { get; private set; }

        public int BufferSize { get; private set; }

        public PlayableMedia? CurrentSong { get; private set; }

        public bool Playing { get; private set; }

        public bool Shuffle { get; private set; }

        public bool Loop { get; private set; }

        public bool Paused { get; set; }

        private bool skip = false;

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

        public IMediaInfo RemoveFromQueue(int index) => songQueue.RemoveAtAsync(index).Result.GetMediaInfo();

        private bool IsAlone() => channel!.GetUsersAsync().First().Result.Count == 1;

        private volatile bool switching = false;

        private Thread WriteToChannel(AudioProcessor audio)
        {
            if (audioClient == null)
                throw new NullReferenceException("Audio Client was null.");

            playbackToken = new CancellationTokenSource();

            bool BreakConditions() => IsAlone() || skip || playbackToken!.IsCancellationRequested;

            void Write()
            {
                var inS = audio.GetOutput();               
                byte[] buffer = new byte[BufferSize];
                int bytesRead;
                try
                {
                    while ((bytesRead = inS!.Read(buffer, 0, buffer.Length)) != 0)
                    {
                        if (BreakConditions())
                            break;

                        while (Paused && !BreakConditions())
                        {
                            Thread.Sleep(1000);
                        }
                        // Check if null
                        discordOut!.Write(buffer, 0, bytesRead);
                    }
                }
                catch (Exception e) when (e is OperationCanceledException ||  // Catch exception when stopping playback
                                          e is System.Net.WebSockets.WebSocketException)
                {
                    // Attempt to reconnect if the WebSocket expires.
                    if (e is System.Net.WebSockets.WebSocketException)
                    {
                        discordOut = audioClient!.CreatePCMStream(AudioApplication.Music, Bitrate, 100, 0);
                        Write();
                    }
                }
                finally
                {
                    audio.Dispose();
                    discordOut!.Flush();
                    Playing = false;
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
            await channel!.DisconnectAsync();
            await discordOut!.DisposeAsync();
            await audioClient!.StopAsync();
            await Task.Run(audioClient.Dispose);
        }

        public Task ToggleLoopAsync(Action<IMediaInfo, bool>? callback = null)
        {
            Loop = !Loop;
            callback?.Invoke(CurrentSong!, Loop);
            return Task.CompletedTask;
        }

        public Task ToggleShuffleAsync(Action<IMediaInfo, bool>? callback = null)
        {
            Shuffle = !Shuffle;
            callback?.Invoke(GetQueue().GetMediaInfo(), Shuffle);
            return Task.CompletedTask;
        }

        public Task StopAsync(bool clearQueue = true)
        {
            Loop = false;
            playbackToken?.Cancel();
            playbackThread?.Join();

            return clearQueue ? songQueue.ClearAsync() : Task.CompletedTask;
        }

        public async Task ConnectToChannelAsync(IAudioChannel channel)
        {
            this.channel = channel;
            audioClient = await channel.ConnectAsync();
        }

        public struct StatusCallbacks
        {
            public Action<IMediaInfo> downloadingCallback;
            public Action<IMediaInfo, bool> playingCallback;
        }

        public async Task PlayAsync(MediaRequest request, IAudioChannel channel, bool switchingPlayback = false, StatusCallbacks? callbacks = null, int bitrate = DefaultBitrate, int bufferSize = DefaultBufferSize)
        {
            switching = switchingPlayback;
            if (Playing && !switching)
            {
                await songQueue.EnqueueAsync(await request.GetRequestsAsync());
                callbacks?.playingCallback(request.GetMediaInfo(), true);
                return;
            }
            else if (Playing && switching)
            {
                Paused = false;
                await StopAsync(false);
            }

            await songQueue.PutFirst(await request.GetRequestsAsync());

            this.channel = channel;

            bool badClient = audioClient == null ||
                             audioClient.ConnectionState == ConnectionState.Disconnected ||
                             audioClient.ConnectionState == ConnectionState.Disconnecting;
            audioClient = badClient ? await channel.ConnectAsync() : audioClient!;
            discordOut = badClient ? audioClient.CreatePCMStream(AudioApplication.Music, Bitrate, 100, 0) : discordOut;

            try
            {
                Playing = true;
                if (!Loop)
                    callbacks?.downloadingCallback(request.GetMediaInfo());
                CurrentSong = await (await songQueue.DequeueAsync()).GetMediaAsync();
            }
            catch (Exception ex)
            {
                Playing = false;
                await DismissAsync().ConfigureAwait(false);
                throw new Exception("Error while downloading video. " + ex.Message);
            }

            if (!Loop)
            {
                callbacks?.playingCallback(request.IsPlaylist ? request.GetMediaInfo() : CurrentSong!, false);
            }

            switching = false;
            playbackThread = WriteToChannel(new AudioProcessor(CurrentSong!.Meta.MediaPath, bitrate, bufferSize / 2, CurrentSong.Meta.Format));
            playbackThread.Start();
            playbackThread.Join();

            if (IsAlone())
            {
                await DismissAsync().ConfigureAwait(false);
                return;
            }

            if (switching)
            {
                return;
            }

            if (!skip && Loop)
            {
                await PlayAsync(new MediaRequest(CurrentSong), channel, false, callbacks).ConfigureAwait(false);
                return;
            }

            if (songQueue.IsEmpty || (playbackToken?.IsCancellationRequested ?? false))
            {
                await DismissAsync().ConfigureAwait(false);
                return;
            }

            skip = false;
            await PlayAsync(Shuffle ? await songQueue.DequeueRandomAsync() : await songQueue.DequeueAsync(), channel, false, callbacks).ConfigureAwait(false);
        }
    }
}
