using CasinoBot.Modules.Jukebox.Models;
using CasinoBot.Utility.Extensions;
using Discord;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace CasinoBot.Modules.Jukebox.Services.Cache
{
    public class MediaCache
    {
        public MediaCache(IGuild guild)
        {
            cacheLocation = Path.Combine(CacheRoot, $"{guild.Name}");
            bool exists = Directory.Exists(cacheLocation);
            if (!exists)
                Directory.CreateDirectory(cacheLocation);
            else
            {
                foreach (Metadata meta in Directory.EnumerateFileSystemEntries(cacheLocation, $"*{CachedMedia.MetaFileExtension}", SearchOption.AllDirectories))
                {
                    cache.Add(new PlayableMedia(meta));
                }
            }
        }

        public const int MaxClearAttempt = 5;

        public const int MaxFilesInCache = 25;

        public const string CacheRoot = @"./mediacache/";

        private readonly List<PlayableMedia> cache = new List<PlayableMedia>();

        private readonly string cacheLocation;

        public Task<long> GetCacheSizeAsync()
        {
            var files = Directory.EnumerateFiles(cacheLocation);
            if (files.Count() == 0)
                return Task.FromResult((long)0);
            return Task.FromResult(files.AsParallel().Convert(x => new FileInfo(x)).Sum(f => f.Length));
        }

        public bool Contains(PlayableMedia med) => cache.Contains(med);

        public bool Contains(string title) => cache.Any(x => x.Meta.Title == title);

        public async Task<bool> PruneCacheAsync(bool forceClear = false)
        {
            if (!forceClear && await GetCacheSizeAsync() < Settings.MaxFileCacheInMB * 1024 * 1024)
                return false;

            Parallel.ForEach(Directory.EnumerateFiles(cacheLocation).Convert(x => new FileInfo(x)), async f =>
            {
                try
                {
                    f.Delete();
                    cache.Remove(await GetAsync(f.Name));
                }
                catch (Exception) { }
            });
            return true;
        }

        public Task<PlayableMedia> GetAsync(string title) => Task.FromResult(cache.Single(x => x.Meta.Title == title));

        public async Task<MediaCollection> CacheMediaAsync(MediaCollection col, bool pruneCache = true)
        {
            if (pruneCache)
                await PruneCacheAsync();

            var pl = col.IsPlaylist;
            var o = new List<PlayableMedia>();
            foreach (var med in col)
            {
                if (Contains(med))
                {
                    o.Add(await GetAsync(med.GetTitle()));
                    continue;
                }

                CachedMedia ca = new CachedMedia(med, cacheLocation);
                o.Add(ca);
                cache.Add(ca);
            }
            return pl ? new MediaCollection(o, col.PlaylistName, col.PlaylistIndex) : new MediaCollection(o.First());
        }
    }
}