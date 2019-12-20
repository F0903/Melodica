using Discord;
using Discord.Audio;
using PokerBot.Modules.Jukebox.Models;
using PokerBot.Modules.Jukebox.Services.Cache;
using PokerBot.Modules.Jukebox.Services.Downloaders;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace PokerBot.Modules.Jukebox
{
    public class JukeboxService
    {
        public JukeboxService(IAsyncMediaCache cache, IAsyncDownloadService downloader)
        {
            songCache = cache;
            this.downloader = downloader;
        }

        private sealed class JukeboxPlayer : IDisposable
        {
            public JukeboxPlayer(int bitrate = DefaultBitrate, int bufferSize = DefaultBufferSize, IAudioChannel channel = null)
            {
                Bitrate = bitrate;
                BufferSize = bufferSize;
                this.channel = channel;
            }

            public int Bitrate { get; private set; }

            public int BufferSize { get; private set; }

            public string CurrentSong { get; private set; }

            public bool Paused { get; set; } = false;

            public bool Looping { get; set; } = false;

            private readonly SongQueue<PlayableMedia> songQueue =
                new SongQueue<PlayableMedia>();

            private IAudioChannel channel;

            private IAudioClient audioClient;

            private bool skip = false;

            private bool playing = false;

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

            public bool IsPlaying() => playing;

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
                callback?.Invoke((media.Name, true));
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

            public async Task PlayAsync(MediaCollection col, Action<(string song, bool queued)> callback = null, int bitrate = JukeboxService.DefaultBitrate, int bufferSize = JukeboxService.DefaultBufferSize)
            {
                PlayableMedia song = col[0];

                if (col.IsPlaylist)
                {
                    for (int i = 0; i < col.Length; i++)
                    {
                        if (i == col.PlaylistIndex - 1)
                            continue;
                        await QueueAsync(col[i]);
                    }
                    song = col[col.PlaylistIndex - 1];
                    callback?.Invoke((col.PlaylistName, true));
                }

                if (playing)
                {
                    await QueueAsync(song, callback);
                    return;
                }

                using var playerOut = (audio = new AudioProcessor(song.Path, bitrate, bufferSize / 2, song.Format)).GetOutput();
                discordOut ??= (audioClient ??= await channel.ConnectAsync()).CreatePCMStream(AudioApplication.Music, Bitrate, 100, 0);

                CurrentSong = song.Name;

                callback?.Invoke((song.Name, false));

                playing = true;
                await WriteToChannelAsync(playerOut);
                playing = false;

                CurrentSong = null;
                audio.Dispose();

                if (playCancel.IsCancellationRequested)
                    return;

                if (!skip && Looping)
                {
                    await PlayAsync(new MediaCollection(song), callback).ConfigureAwait(false);
                    return;
                }

                if (!songQueue.IsEmpty)
                {
                    await PlayAsync(new MediaCollection(await songQueue.DequeueAsync()), callback).ConfigureAwait(false);
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

        public const int DefaultBitrate = 128 * 1024;
        public const int DefaultBufferSize = 1 * 1024;

        private readonly IAsyncMediaCache songCache;

        private readonly IAsyncDownloadService downloader;

        private readonly JukeboxDictionary<IGuild, JukeboxPlayer> jukeboxes = new JukeboxDictionary<IGuild, JukeboxPlayer>();

        public async Task LeaveChannelAsync(IGuild guild) =>
            await jukeboxes[guild].LeaveChannelAsync();

        public string GetPlayingSong(IGuild guild) =>
            jukeboxes[guild].CurrentSong;

        public void SetPause(IGuild guild, bool val) =>
            jukeboxes[guild].Paused = val;

        public void SetLooping(IGuild guild, bool val) =>
            jukeboxes[guild].Looping = val;

        public bool IsLooping(IGuild guild) =>
            jukeboxes[guild].Looping;

        public void Skip(IGuild guild) => jukeboxes[guild].Skip();

        public Task ClearQueueAsync(IGuild guild) => jukeboxes[guild].ClearQueueAsync();

        public Task<PlayableMedia> RemoveFromQueueAsync(IGuild guild, int i) =>
            jukeboxes[guild].RemoveFromQueueAsync(i);

        public Task<PlayableMedia[]> GetQueueAsync(IGuild guild) =>
            Task.FromResult(jukeboxes[guild].GetQueue());

        private async Task<JukeboxPlayer> JoinChannelInternal(IGuild guild, IAudioChannel channel)
        {
            var jukebox = new JukeboxPlayer();
            await jukebox.ConnectToChannelAsync(channel);
            jukeboxes.AddEntry(guild, jukebox);
            return jukebox;
        }

        public async Task JoinChannelAsync(IGuild guild, IAudioChannel channel) =>
            await JoinChannelInternal(guild, channel);

        public async Task PlayAsync(IGuild guild, IAudioChannel channel, string searchQuery, Action<(string song, bool putInQueue)> playCallback = null)
        {
            if (!jukeboxes.TryGetEntry(guild, out var jukebox))
                jukebox = await JoinChannelInternal(guild, channel);

            var res = await downloader.DownloadAsync(songCache, searchQuery);

            await jukebox.PlayAsync(res, playCallback).ConfigureAwait(false);
        }

        public Task StopAsync(IGuild guild)
        {
            if (!jukeboxes.TryGetEntry(guild, out var jukebox))
                throw new Exception("Could not get value out of cache.");
            return jukebox.StopAsync();
        }
    }
}
