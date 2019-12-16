using Discord;
using Discord.Audio;
using PokerBot.Models;
using PokerBot.Services.Cache;
using PokerBot.Services.Downloaders;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Tasks;

namespace PokerBot.Services.Jukebox
{
    public static class Jukebox
    {
        public const int DefaultBitrate = 128 * 1024;
        public const int DefaultBufferSize = 1 * 1024;

        private static readonly AsyncMediaFileCache songCache = new AsyncMediaFileCache();

        private static readonly AsyncYoutubeDownloader yt = new AsyncYoutubeDownloader();

        private static readonly JukeboxDictionary<IGuild, JukeboxPlayer> jukeboxes = new JukeboxDictionary<IGuild, JukeboxPlayer>();

        public static async Task LeaveChannelAsync(IGuild guild) =>
            await jukeboxes[guild].LeaveChannelAsync();

        public static string GetPlayingSong(IGuild guild) =>
            jukeboxes[guild].CurrentSong;

        public static void SetPause(IGuild guild, bool val) =>
            jukeboxes[guild].Paused = val;

        public static void SetLooping(IGuild guild, bool val) =>
            jukeboxes[guild].Looping = val;

        public static void Skip(IGuild guild) => jukeboxes[guild].Skip();

        public static Task<PlayableMedia[]> GetQueueAsync(IGuild guild) =>
            Task.FromResult(jukeboxes[guild].GetQueue());

        private static async Task<JukeboxPlayer> JoinChannelInternal(IGuild guild, IAudioChannel channel)
        {
            var jukebox = new JukeboxPlayer();
            await jukebox.ConnectToChannelAsync(channel);
            jukeboxes.AddEntry(guild, jukebox);
            return jukebox;
        }

        public static async Task JoinChannelAsync(IGuild guild, IAudioChannel channel) =>
            await JoinChannelInternal(guild, channel);

        public static async Task PlayAsync(IGuild guild, IAudioChannel channel, string searchQuery, Action<(string song, bool putInQueue)> playCallback = null)
        {
            if (!jukeboxes.TryGetEntry(guild, out var jukebox))
                jukebox = await JoinChannelInternal(guild, channel);

            var res = await yt.DownloadAsync(songCache, searchQuery);

            await jukebox.PlayAsync(res, playCallback).ConfigureAwait(false);            
        }

        public static Task StopAsync(IGuild guild)
        {
            if (!jukeboxes.TryGetEntry(guild, out var jukebox))
                throw new Exception("Could not get value out of cache.");

            jukebox.Dispose();

            jukeboxes.RemoveEntry(guild);
            return Task.CompletedTask;
        }
    }
}
