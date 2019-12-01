using Discord;
using Discord.Audio;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using PokerBot.Entities;
using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace PokerBot.Services
{
    public class StandardJukebox : BaseJukebox
    {
        class Jukebox : IDisposable
        {
            public Jukebox(IAudioChannel channel = null)
            {
                this.channel = channel;
            }

            public string CurrentSong { get; private set; }

            public bool Paused { get; set; } = false;

            private IAudioChannel channel;

            private IAudioClient audioClient;

            private Player player;

            private bool stopped = false;

            public string GetChannelName() => channel.Name;

            public bool IsInChannel() => GetChannelName() != null;

            public bool SetStopped(bool val) => stopped = val;

            public async Task ConnectToChannelAsync(IAudioChannel channel = null)
            {
                this.channel = channel ?? this.channel;
                audioClient = await channel.ConnectAsync();
            }

            public async Task LeaveChannelAsync()
            {
                await channel.DisconnectAsync();
            }

            public async Task WriteToChannelAsync(string songPath, string songName)
            {
                using var playerOut = (player ??= CreatePlayer(songPath)).GetStdOut();
                using var discordOut = (audioClient ??= await channel.ConnectAsync()).CreatePCMStream(AudioApplication.Music, Bitrate, Bitrate / 100, 0);

                CurrentSong = songName;

                byte[] buffer = new byte[BufferSize];
                int bytesRead = 0;
                while ((bytesRead = await playerOut.ReadAsync(buffer, 0, buffer.Length)) != 0)
                {
                    while (Paused) 
                    {
                        if (stopped)
                            break;
                    }

                    if (stopped)
                    {
                        await playerOut.FlushAsync();
                    }

                    await discordOut.WriteAsync(buffer, 0, bytesRead);
                }
                CurrentSong = null;
            }

            public void Dispose()
            {
                SetStopped(true);
                player.Stop();
                channel.DisconnectAsync();
                audioClient.Dispose();
            }
        }

        public StandardJukebox(AsyncYoutubeDownloader yt, AsyncFileCache cache)
        {
            this.yt = yt;
            this.songCache = cache;
        }

        private readonly AsyncFileCache songCache;

        private readonly AsyncYoutubeDownloader yt;

        private static MemoryCache jukeboxCache = new MemoryCache(new MemoryCacheOptions());

        private Task<Jukebox> JoinChannelInternal(IGuild guild, IAudioChannel channel)
        {
            var jukebox = new Jukebox(channel);
            jukeboxCache.Set(guild, jukebox);
            return Task.FromResult(jukebox);
        }

        public async Task LeaveChannelAsync(IGuild guild) =>
            await jukeboxCache.Get<Jukebox>(guild).LeaveChannelAsync();

        public string GetPlayingSong(IGuild guild) =>
            jukeboxCache.Get<Jukebox>(guild).CurrentSong;

        public void SetPause(IGuild guild, bool val) =>
            jukeboxCache.Get<Jukebox>(guild).Paused = val;

        public async Task JoinChannelAsync(IGuild guild, IAudioChannel channel) =>      
            await JoinChannelInternal(guild, channel);
        
        public async Task PlayAsync(IGuild guild, IAudioChannel channel, string searchQuery, Action<string> playCallback = null)
        {
            if (!jukeboxCache.TryGetValue<Jukebox>(guild, out var jukebox))
                jukebox = await JoinChannelInternal(guild, channel);

            var (path, song) = await yt.DownloadToCache(songCache, searchQuery);

            playCallback?.Invoke(song);
            await jukebox.WriteToChannelAsync(path, song);
        }            

        public Task StopAsync(IGuild guild)
        {
            if (!jukeboxCache.TryGetValue<Jukebox>(guild, out var jukebox))
                throw new Exception("Could not get value out of memcache.");

            jukebox.Dispose();

            jukeboxCache.Remove(guild);
            return Task.CompletedTask;
        }
    }
}
