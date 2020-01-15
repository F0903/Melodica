using Discord;
using Discord.Audio;
using CasinoBot.Modules.Jukebox.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CasinoBot.Modules.Jukebox.Services.Cache;
using CasinoBot.Modules.Jukebox.Services.Downloaders;
using CasinoBot.Modules.Jukebox.Models.Requests;

namespace CasinoBot.Modules.Jukebox
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

        public string CurrentSong { get; private set; }

        public bool Playing { get; private set; } = false;

        public bool Paused { get; set; } = false;

        public bool Looping { get; set; } = false;

        public bool Shuffle { get; set; } = false;

        private readonly SongQueue<PlayableMedia> songQueue =
            new SongQueue<PlayableMedia>();

        private readonly MediaCache cache;

        private IAudioChannel channel;

        private IAudioClient audioClient;

        private bool skip = false;

        private bool stop = false;

        private AudioOutStream discordOut;

        private async Task WriteToChannelAsync(AudioProcessor audio)
        {
            if (audioClient == null)
                throw new NullReferenceException("Audio Client was null.");

            var inS = audio.GetOutput();

            byte[] buffer = new byte[BufferSize];
            int bytesRead;
            try
            {
                while ((bytesRead = await inS.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false)) != 0)
                {
                    while (Paused)
                    {
                        if (skip || stop)
                            break;
                        await Task.Delay(1000);
                    }

                    if (skip || stop)
                        break;
                    await discordOut.WriteAsync(buffer, 0, bytesRead).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) { } // Catch exception when stopping playback
            finally
            {
                await inS.FlushAsync();
                await audio.DisposeAsync();
                await discordOut.FlushAsync();
            }
            skip = false;
        }

        public MediaCache GetCache() => cache;

        public string GetChannelName() => channel.Name;

        public bool IsInChannel() => GetChannelName() != null;

        public void Skip() => skip = true;

        public PlayableMedia[] GetQueue() => songQueue.ToArray();

        public Task ClearQueueAsync() => songQueue.ClearAsync();

        public Task<PlayableMedia> RemoveFromQueueAsync(int index) => songQueue.RemoveAtAsync(index);

        public Task StopAsync()
        {
            stop = true;
            return songQueue.ClearAsync();
        }

        public async Task QueueAsync(PlayableMedia media)
        {
            if (!File.Exists(media.Path))
                throw new Exception("Specified song path to queue is empty.");

            await songQueue.EnqueueAsync(media);
        }

        private async Task QueueAsync(MediaCollection playlist, int startIndex = 0)
        {
            var temp = playlist.ToList();
            temp.RemoveAt(startIndex);
            var toQueue = temp.ToArray();
            await songQueue.UnsafeEnqueueAsync(toQueue); // Experimental. DELETE IF NOT WORKING.
        }

        public async Task ConnectToChannelAsync(IAudioChannel channel)
        {
            this.channel = channel;
            audioClient = await channel.ConnectAsync();
        }

        public async Task LeaveChannelAsync()
        {
            await channel.DisconnectAsync();
        }

        public async Task PlayAsync(MediaRequest request, IAudioChannel channel, bool switchingPlayback = false, Action<(string song, bool queued)> playingCallback = null, int bitrate = DefaultBitrate, int bufferSize = DefaultBufferSize)
        {
            MediaCollection col = await request.GetMediaRequestAsync();
            PlayableMedia song = col[col.PlaylistIndex - 1]; // PlaylistIndex starts at 1, so decrease it.

            if (col.IsPlaylist)
            {
                await QueueAsync(col, col.PlaylistIndex - 1);

                playingCallback?.Invoke((col.PlaylistName, true));
                if (Playing && !switchingPlayback)
                    return;
            }

            if (switchingPlayback)
            {
                await StopAsync();
            }

            if (Playing && !switchingPlayback)
            {
                await QueueAsync(song).ConfigureAwait(false);
                playingCallback?.Invoke((song.Title, true));
                return;
            }

            this.channel = channel;;

            bool newClient = audioClient                 == null                         ||
                             audioClient.ConnectionState == ConnectionState.Disconnected ||
                             audioClient.ConnectionState == ConnectionState.Disconnecting;
            audioClient = newClient ? await channel.ConnectAsync() : audioClient;
            discordOut = newClient ? audioClient.CreatePCMStream(AudioApplication.Music, Bitrate, 100, 0) : discordOut;

            CurrentSong = song.Title;

            playingCallback?.Invoke((song.Title, false));          
            Playing = true;
            switchingPlayback = false;
            await WriteToChannelAsync(new AudioProcessor(song.Path, bitrate, bufferSize / 2, song.Format));
            if (switchingPlayback)
                return;
            Playing = false;

            CurrentSong = null;
            
            if (stop)
            {
                stop = false;
                return;
            }

            if (!skip && Looping)
            {
                await PlayAsync(request, channel, false, playingCallback).ConfigureAwait(false);
                return;
            }

            if (!songQueue.IsEmpty)
            {
                await PlayAsync(new MediaRequest(new MediaCollection(Shuffle ? await songQueue.DequeueRandomAsync() : await songQueue.DequeueAsync())), channel, false, playingCallback).ConfigureAwait(false);
                return;
            }
        }
    }
}
