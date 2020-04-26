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

        public Metadata RemoveFromQueue(int index) => songQueue.RemoveAtAsync(index).Result.GetMediaInfo();

        private bool IsAlone() => channel!.GetUsersAsync().First().Result.Count == 1;

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
                    Playing = true;
                    while ((bytesRead = inS!.Read(buffer, 0, buffer.Length)) != 0 || audio.isLivestream)
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
            await channel!.DisconnectAsync();
            await discordOut!.DisposeAsync();
            await audioClient!.StopAsync();
            await Task.Run(audioClient.Dispose);
        }

        public Task ToggleLoopAsync(Action<(Metadata info, bool wasLooping)>? callback = null)
        {
            if (CurrentSong == null)
                throw new Exception("No song is playing.");

            if (CurrentSong!.Info.Format == "hls")
                throw new Exception("Can't loop a livestream.");

            callback?.Invoke((CurrentSong!.Info, Loop));
            Loop = !Loop;
            return Task.CompletedTask;
        }

        public Task ToggleShuffleAsync(Action<Metadata, bool>? callback = null)
        {
            callback?.Invoke(GetQueue().GetMediaInfo(), Shuffle);
            Shuffle = !Shuffle;
            return Task.CompletedTask;
        }

        public Task StopAsync(bool clearQueue = true)
        {
            Loop = false;
            Paused = false;
            playbackToken?.Cancel();
            playbackThread?.Join();

            return clearQueue ? songQueue.ClearAsync() : Task.CompletedTask;
        }

        public async Task ConnectToChannelAsync(IAudioChannel channel)
        {
            this.channel = channel;
            audioClient = await channel.ConnectAsync();
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

        public struct StatusCallbacks
        {
            public Action<(Metadata info, bool finished)> downloadingCallback;
            public Action<((Metadata songInfo, Metadata? playlistInfo)? infoSet, MediaType type, (bool queued, bool downloaded) state)> playingCallback;
        }

        public async Task PlayAsync(MediaRequest request, IAudioChannel channel, bool switchSong = false, bool loop = false, StatusCallbacks? callbacks = null)
        {
            if (loop && request.Type == MediaType.Livestream)
                throw new Exception("Can't loop a livestream.");

            Loop = loop;
            switching = switchSong;

            await Connect(channel);
            bool wasPlaying = Playing;
          
            var requests = await request.GetRequestsAsync();
            if (Playing && !switching)
            {
                await songQueue.EnqueueAsync(requests);
                callbacks?.playingCallback?.Invoke(((request.GetMediaInfo(), null), request.Type, (true, false)));
                return;
            }

            await songQueue.PutFirst(requests);

            //TODO: Fix "downloading" message.
            //if (!Loop && request.Type != MediaType.Livestream)
            //    callbacks?.playingCallback?.Invoke((null, request.Type, (false, false)));
            try
            {
                Playing = true; // Set playing to true so nobody is able to switch while a song is downloading.
                CurrentSong = await (await songQueue.DequeueAsync()).GetMediaAsync();
            }
            catch (Exception ex)
            {
                Playing = false;
                Shuffle = false;
                throw new Exception("Error while downloading video. " + ex.Message);
            }

            if (!Loop && request.Type == MediaType.Playlist)
                callbacks?.playingCallback?.Invoke(((CurrentSong.Info, request.Type == MediaType.Playlist ? request.GetMediaInfo() : null), request.Type, (false, true)));           

            if (!Loop || (Loop && switching))
                callbacks?.playingCallback?.Invoke(((CurrentSong!.Info, request.Type == MediaType.Playlist ? request.GetMediaInfo() : null), request.Type, (false, true)));

            if (switching)
            {
                await StopAsync(false);
                Loop = loop;                
            }
            
            playbackThread = WriteToChannel(new AudioProcessor(CurrentSong!.Info.MediaPath, BufferSize, CurrentSong.Info.Format));
            playbackThread.Start();
            playbackThread.Join();

            if (switching && !(playbackToken?.IsCancellationRequested ?? true))
            {
                switching = false;
                return;
            }

            if (IsAlone())
            {
                await DismissAsync().ConfigureAwait(false);
                return;
            }

            if (!skip && Loop)
            {
                await PlayAsync(new MediaRequest(CurrentSong), channel, false, loop, callbacks).ConfigureAwait(false);
                return;
            }

            if (songQueue.IsEmpty || (playbackToken?.IsCancellationRequested ?? true))
            {
                Loop = false;
                await DismissAsync().ConfigureAwait(false);
                return;
            }

            skip = false;
            await PlayAsync(Shuffle ? await songQueue.DequeueRandomAsync() : await songQueue.DequeueAsync(), channel, false, loop, callbacks).ConfigureAwait(false);
        }
    }
}
