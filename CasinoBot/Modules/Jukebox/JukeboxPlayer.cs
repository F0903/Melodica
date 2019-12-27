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
using CasinoBot.Modules.Jukebox.Models.Requests;

namespace CasinoBot.Modules.Jukebox
{
    public sealed class JukeboxPlayer : IDisposable
    {
        public JukeboxPlayer(IGuild guild, IAsyncMediaCache cache, int bitrate = DefaultBitrate, int bufferSize = DefaultBufferSize)
        {
            Bitrate = bitrate;
            BufferSize = bufferSize;
            this.connectedGuild = guild;
            this.cache = cache;
            cache.Init(guild.Name);
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

        private readonly IGuild connectedGuild;

        private IAudioChannel channel;

        private IAudioClient audioClient;

        private readonly IAsyncMediaCache cache;

        private bool skip = false;

        private AudioOutStream discordOut;

        private AudioProcessor audio;

        private CancellationTokenSource playCancel;

        private async Task WriteToChannelAsync(Stream input)
        {
            playCancel = new CancellationTokenSource();
            byte[] buffer = new byte[BufferSize];
            int bytesRead;
            try
            {
                while ((bytesRead = await input.ReadAsync(buffer, 0, buffer.Length, playCancel.Token).ConfigureAwait(false)) != 0)
                {
                    while (Paused)
                    {
                        if (skip)
                            break;
                        await Task.Delay(1000);
                    }

                    if (skip)
                        break;
                    await discordOut.WriteAsync(buffer, 0, bytesRead, playCancel.Token).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) { } // Catch exception when stopping playback
            skip = false;
        }
        public string GetChannelName() => channel.Name;

        public bool IsInChannel() => GetChannelName() != null;

        public void Skip() => skip = true;

        public PlayableMedia[] GetQueue() => songQueue.ToArray();

        public Task ClearQueueAsync() => songQueue.ClearAsync();

        public Task<PlayableMedia> RemoveFromQueueAsync(int index) => songQueue.RemoveAtAsync(index);

        public async Task StopAsync()
        {
            playCancel?.Cancel(false);
            playCancel?.Dispose();
            await songQueue.ClearAsync();
        }

        public async Task QueueAsync(PlayableMedia media, Action<(string song, bool queued)> callback = null)
        {
            if (!File.Exists(media.Path))
                throw new Exception("Specified song path to queue is empty.");

            await songQueue.EnqueueAsync(media);
            callback?.Invoke((media.Title, true));
        }

        private async Task QueueAsync(MediaCollection playlist, params int[] exludeElements)
        {
            PlayableMedia[] toQueue = null;
            foreach (var i in exludeElements)
            {
                var temp = playlist.ToList();
                temp.RemoveAt(i);
                toQueue = temp.ToArray();
            }
            await songQueue.EnqueueAsync(toQueue);
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

        public async Task PlayAsync(IRequest request, IAudioChannel channel, Action<(string song, bool queued)> playingCallback = null, Action largeSizeCallback = null, Action<string> unavailableCallback = null, int bitrate = DefaultBitrate, int bufferSize = DefaultBufferSize)
        {
            MediaCollection col = null;
            if (request.IsDownloadRequest)
                col = await request.GetDownloader().DownloadToCacheAsync(cache, connectedGuild.Name, (string)request.GetRequest(), true, largeSizeCallback, unavailableCallback);
            else
                col = (MediaCollection)request.GetRequest();
            PlayableMedia song = col[col.PlaylistIndex - 1]; // PlaylistIndex starts at 1, so decrease it.

            if (col.IsPlaylist)
            {
                await Task.Run(() => QueueAsync(col, col.PlaylistIndex - 1));

                playingCallback?.Invoke((col.PlaylistName, true));
                if (Playing)
                    return;
            }

            if (Playing)
            {
                await QueueAsync(song, playingCallback).ConfigureAwait(false);
                return;
            }

            this.channel = channel;

            using var playerOut = (audio = new AudioProcessor(song.Path, bitrate, bufferSize / 2, song.Format)).GetOutput();
            discordOut ??= (audioClient ??= await channel.ConnectAsync()).CreatePCMStream(AudioApplication.Music, Bitrate, 100, 0);

            CurrentSong = song.Title;

            playingCallback?.Invoke((song.Title, false));

            Playing = true;
            await WriteToChannelAsync(playerOut);
            Playing = false;

            CurrentSong = null;
            audio.Dispose();

            if (playCancel.IsCancellationRequested)
                return;

            if (!skip && Looping)
            {
                await PlayAsync(request, channel, playingCallback).ConfigureAwait(false);
                return;
            }

            if (!songQueue.IsEmpty)
            {
                await PlayAsync(new ExistingMediaRequest(new MediaCollection(Shuffle ? await songQueue.DequeueRandomAsync() : await songQueue.DequeueAsync())), channel, playingCallback).ConfigureAwait(false);
                return;
            }
        }
        
        public void Dispose()
        {
            StopAsync().Wait();
            audio?.Dispose();
            audio = null;
            channel?.DisconnectAsync();
            channel = null;
            audioClient?.Dispose();
            audioClient = null;
        }
    }
}
