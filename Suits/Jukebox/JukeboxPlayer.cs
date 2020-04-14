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

        public int Bitrate { get; set; }

        public int BufferSize { get; set; }

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

        private AudioOutStream CreateOutputStream() => audioClient!.CreatePCMStream(AudioApplication.Music, Bitrate, 100, 0);

        private Thread WriteToChannel(AudioProcessor audio)
        {
            if (audioClient == null)
                throw new NullReferenceException("Audio Client was null.");

            playbackToken = new CancellationTokenSource();

            bool BreakConditions() => IsAlone() || skip || playbackToken!.IsCancellationRequested;

            void Write() // Refactor
            {
                var inS = audio.GetOutput();
                byte[] buffer = new byte[BufferSize];
                int bytesRead;
                try
                {
                    while ((bytesRead = inS!.Read(buffer, 0, buffer.Length)) != 0 || audio.isLivestream)
                    {
                        var shouldBreak = BreakConditions();

                        while (Paused && !shouldBreak)
                        {
                            Thread.Sleep(1000);
                        }

                        if (shouldBreak)
                            break;

                        if (discordOut == null)
                            throw new NullReferenceException("Unknown error occured during playback.");
                        try
                        {
                            discordOut!.Write(buffer, 0, bytesRead);
                        }
                        catch
                        {
                            discordOut = CreateOutputStream();
                        }
                    }
                }
                catch (Exception e) when (e is OperationCanceledException ||  // Catch exception when stopping playback
                                          e is System.Net.WebSockets.WebSocketException)
                {
                    if (e is OperationCanceledException)
                        return;
                    // Attempt to reconnect if the WebSocket expires.
                    if (e is System.Net.WebSockets.WebSocketException)
                    {
                        discordOut = CreateOutputStream();
                        Write();
                    }
                    throw e;
                }
                finally
                {
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
            await channel!.DisconnectAsync();
            await discordOut!.DisposeAsync();
            await audioClient!.StopAsync();
            await Task.Run(audioClient.Dispose);
        }

        public Task ToggleLoopAsync(Action<(IMediaInfo info, bool wasLooping)>? callback = null)
        {
            callback?.Invoke((CurrentSong!, Loop));
            Loop = !Loop;
            return Task.CompletedTask;
        }

        public Task ToggleShuffleAsync(Action<IMediaInfo, bool>? callback = null)
        {
            callback?.Invoke(GetQueue().GetMediaInfo(), Shuffle);
            Shuffle = !Shuffle;
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
            public Action<(IMediaInfo info, bool queued)> playingCallback;
            public Action<(IMediaInfo playlistInfo, IMediaInfo currentSong)> playingPlaylistCallback;
        }

        private async Task Connect(IAudioChannel channel)
        {
            bool badClient = audioClient == null ||
                             audioClient.ConnectionState == ConnectionState.Disconnected ||
                             audioClient.ConnectionState == ConnectionState.Disconnecting;
            audioClient = badClient ? await channel.ConnectAsync() : audioClient!;
            discordOut = badClient ? CreateOutputStream() : discordOut;
            this.channel = channel;
        }

        // Refactor
        public async Task PlayAsync(MediaRequest request, IAudioChannel channel, bool switchingPlayback = false, bool loop = false, StatusCallbacks? callbacks = null)
        {
            await Connect(channel);
            bool wasPlaying = Playing;

            Loop = loop;
            switching = switchingPlayback;
            var requests = await request.GetRequestsAsync();
            if (Playing && !switching)
            {
                await songQueue.EnqueueAsync(requests);
                callbacks?.playingCallback?.Invoke((request.GetMediaInfo(), true));
                return;
            }

            await songQueue.PutFirst(requests);

            if (!Loop)
                callbacks?.downloadingCallback?.Invoke(request.GetMediaInfo());
            try
            {
                Playing = true;
                CurrentSong = await (await songQueue.DequeueAsync()).GetMediaAsync();
            }
            catch (Exception ex)
            {
                Playing = false;
                Shuffle = false;
                throw new Exception("Error while downloading video. " + ex.Message);
            }

            if (switching)
            {
                Paused = false;
                if (wasPlaying)
                    await StopAsync(false);
                Playing = true;
            }

            if (!Loop || (Loop && switching))
            {
                if (request.IsPlaylist)
                    callbacks?.playingPlaylistCallback?.Invoke((request.GetMediaInfo(), CurrentSong));
                else
                    callbacks?.playingCallback?.Invoke((CurrentSong!, false));
            }

            playbackThread = WriteToChannel(new AudioProcessor(CurrentSong!.Meta.MediaPath, BufferSize, CurrentSong.Meta.FileFormat));
            playbackThread.Start();
            playbackThread.Join();
            Playing = false;

            if (IsAlone())
            {
                await DismissAsync().ConfigureAwait(false);
                return;
            }

            if (switching)
            {
                switching = false;
                return;
            }

            if (!skip && Loop)
            {
                await PlayAsync(new MediaRequest(CurrentSong), channel, false, loop, callbacks).ConfigureAwait(false);
                return;
            }

            if (songQueue.IsEmpty || (playbackToken?.IsCancellationRequested ?? false))
            {
                await DismissAsync().ConfigureAwait(false);
                return;
            }

            skip = false;
            await PlayAsync(Shuffle ? await songQueue.DequeueRandomAsync() : await songQueue.DequeueAsync(), channel, false, loop, callbacks).ConfigureAwait(false);
        }

        public async Task PlayLivestreamAsync(BaseLivestreamRequest request, IAudioChannel channel, Action<IMediaInfo>? startedPlaying = null)
        {
            await Connect(channel);
            if (Playing)
                await StopAsync(true);

            Playing = true;
            startedPlaying?.Invoke(await request.GetInfoAsync());

            playbackThread = WriteToChannel(new AudioProcessor(await request.GetHLSUrlAsync(), BufferSize));
            playbackThread.Start();
            playbackThread.Join();
            if (switching)
                return;

            Playing = false;

            await DismissAsync().ConfigureAwait(false);
        }
    }
}
