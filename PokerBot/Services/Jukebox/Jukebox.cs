﻿using Discord;
using Discord.Audio;
using PokerBot.Entities;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Tasks;

namespace PokerBot.Services.Jukebox
{
    public class Jukebox
    {
        public const int DefaultBitrate = 128 * 1024;
        public const int DefaultBufferSize = 4 * 1024;

        private static readonly AsyncFileCache songCache = new AsyncFileCache();

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

        public static async Task QueueAsync(IGuild guild, string songName, Action<string> queueCallback = null)
        {
            (var path, var song) = await yt.DownloadToCache(songCache, songName);
            queueCallback?.Invoke(song);
            jukeboxes[guild].Queue(song, path);
        }

        public static Task<(string songName, string songPath)[]> GetQueueAsync(IGuild guild) =>
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

        public static async Task PlayAsync(IGuild guild, IAudioChannel channel, string searchQuery, Action<string> playCallback = null)
        {
            if (!jukeboxes.TryGetEntry(guild, out var jukebox))
                jukebox = await JoinChannelInternal(guild, channel);

            var (path, song) = await yt.DownloadToCache(songCache, searchQuery);

            playCallback?.Invoke(song);
            await jukebox.PlayAsync(path, song);
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