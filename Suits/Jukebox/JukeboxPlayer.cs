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
        public JukeboxPlayer(MediaCache cache, int bitrate = DefaultBitrate, int bufferSize = DefaultBufferSize)
        {
            this.cache = cache;
            Bitrate = bitrate;
            BufferSize = bufferSize;
        }

        public const int DefaultBitrate = 128 * 1024;
        public const int DefaultBufferSize = 1 * 1024;

        public int Bitrate { get; private set; }

        public int BufferSize { get; private set; }

        public PlayableMedia CurrentSong { get; private set; }

        public bool Playing { get; private set; } = false;

        public bool Paused { get; set; } = false;

        public bool Shuffle { get; set; } = false;

        //TODO: Perhaps outsource this to the callee via constructor.
        private readonly SongQueue songQueue = new SongQueue();

        private readonly MediaCache cache;

        private IAudioChannel channel;

        private IAudioClient audioClient;

        private bool skip = false;

        private bool stop = false;

        private bool loop = false;

        private bool switching = false;

        private AudioOutStream discordOut;

        private Task writeTask;

        public MediaCache GetCache() => cache;

        public string GetChannelName() => channel.Name;

        public bool IsInChannel() => GetChannelName() != null;

        public bool IsLooping() => loop;

        public void Skip() => skip = true;

        public SongQueue GetQueue() => songQueue;

        public Task ClearQueueAsync() => songQueue.ClearAsync();

        public Task<PlayableMedia> RemoveFromQueueAsync(int index) => songQueue.RemoveAtAsync(index);

        private async Task WriteToChannelAsync(AudioProcessor audio)
        {
            if (audioClient == null)
                throw new NullReferenceException("Audio Client was null.");
            Playing = true;

            var inS = audio.GetOutput();

            byte[] buffer = new byte[BufferSize];
            int bytesRead;
            try
            {
                while ((bytesRead = await inS.ReadAsync(buffer, 0, buffer.Length)) != 0)
                {
                    while (Paused)
                    {
                        if (skip || stop)
                            break;
                        await Task.Delay(1000);
                    }

                    if (skip || stop)
                        break;
                    await discordOut.WriteAsync(buffer, 0, bytesRead);
                }
            }
            catch (OperationCanceledException) { } // Catch exception when stopping playback
            finally
            {
                await audio.DisposeAsync();
                await discordOut.FlushAsync();
            }
            skip = false;
            Playing = false;
        }

        private async Task DismissAsync()
        {
            await channel.DisconnectAsync();
            await discordOut.DisposeAsync();
            await audioClient.StopAsync();
            await Task.Run(audioClient.Dispose);
        }       

        public Task SetLoopAsync(bool val, Action<IMediaInfo, bool> loopCallback = null)
        {
            loop = val;
            loopCallback?.Invoke(CurrentSong, val);
            return Task.CompletedTask;
        }

        public Task StopAsync(bool clearQueue = true)
        {
            stop = true;
            writeTask.Wait();
            stop = false;
            loop = false;
            return clearQueue ? songQueue.ClearAsync() : Task.CompletedTask;
        }

        public async Task QueueAsync(PlayableMedia media)
        {
            if (!File.Exists(media.Meta.MediaPath))
                throw new Exception("Specified song path to queue is empty.");

            await songQueue.EnqueueAsync(media);
        }

        private async Task QueueAsync(MediaCollection playlist, int startIndex = 0)
        {
            var temp = playlist.ToList();
            temp.RemoveAt(startIndex);
            var toQueue = temp.ToArray();
            await songQueue.UnsafeEnqueueAsync(toQueue);
        }

        public async Task ConnectToChannelAsync(IAudioChannel channel)
        {
            this.channel = channel;
            audioClient = await channel.ConnectAsync();
        }

        public async Task PlayAsync(MediaRequest request, IAudioChannel channel, bool switchingPlayback = false, Action<(IMediaInfo media, bool queued)> playingCallback = null, int bitrate = DefaultBitrate, int bufferSize = DefaultBufferSize)
        {
            MediaCollection col = await request.GetMediaRequestAsync();
            PlayableMedia song = col[col.PlaylistIndex];

            if (col.IsPlaylist)
            {
                await QueueAsync(col, col.PlaylistIndex);

                playingCallback?.Invoke((col, true));
                if (Playing && !switchingPlayback)
                    return;
            }

            if (Playing && switchingPlayback)
            {
                switching = switchingPlayback;
                await StopAsync(false);
            }

            if (Playing && !switchingPlayback)
            {
                await QueueAsync(song);
                playingCallback?.Invoke((song, true));
                return;
            }

            this.channel = channel;

            bool newClient = audioClient == null ||
                             audioClient.ConnectionState == ConnectionState.Disconnected ||
                             audioClient.ConnectionState == ConnectionState.Disconnecting;
            audioClient = newClient ? await channel.ConnectAsync() : audioClient;
            discordOut = newClient ? audioClient.CreatePCMStream(AudioApplication.Music, Bitrate, 100, 0) : discordOut;

            CurrentSong = song;

            if (!loop)
                playingCallback?.Invoke((song, false));

            await (writeTask = WriteToChannelAsync(new AudioProcessor(song.Meta.MediaPath, bitrate, bufferSize / 2, song.Meta.Format)));
            if (switching)
            {
                switching = false;
                return;
            }

            CurrentSong = null;

            if (!stop && !skip && loop)
            {
                await PlayAsync(request, channel, false, playingCallback).ConfigureAwait(false);
                return;
            }

            if (!stop && !songQueue.IsEmpty)
            {
                await PlayAsync(new MediaRequest(new MediaCollection(Shuffle ? await songQueue.DequeueRandomAsync() : await songQueue.DequeueAsync())), channel, false, playingCallback).ConfigureAwait(false);
                return;
            }
            await DismissAsync().ConfigureAwait(false);
        }
    }
}
