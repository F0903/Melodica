using CasinoBot.Jukebox.Models;
using CasinoBot.Utility.Extensions;
using Discord;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace CasinoBot.Jukebox.Services.Cache
{
    public class MediaCache
    {
        public MediaCache(IGuild guild)
        {
            localCache = Path.Combine(CacheRoot, $"{guild.Name}");
            bool exists = Directory.Exists(localCache);
            if (!exists) Directory.CreateDirectory(localCache);
            else LoadPreexistingFilesAsync();
        }

        public const int MaxClearAttempt = 5;

        public const int MaxFilesInCache = 25;

        public const string CacheRoot = @"./mediacache/";
        public readonly string localCache;

        private readonly ConcurrentDictionary<string, string> cache = new ConcurrentDictionary<string, string>();

        private Task LoadPreexistingFilesAsync()
        {
            foreach (FileInfo metaFile in Directory.EnumerateFileSystemEntries(localCache, $"*{Metadata.MetaFileExtension}", SearchOption.AllDirectories).Convert(x => new FileInfo(x)))
            {
                if (!cache.TryAdd(Path.GetFileNameWithoutExtension(metaFile.Name), metaFile.FullName))
                    throw new Exception("Could not add pre-existing file to cache.");
            }
            return Task.CompletedTask;
        }

        public Task<long> GetCacheSizeAsync()
        {
            var files = Directory.EnumerateFiles(localCache);
            if (files.Count() == 0)
                return Task.FromResult((long)0);
            return Task.FromResult(files.AsParallel().Convert(x => new FileInfo(x)).Sum(f => f.Length));
        }

        public bool Contains(PlayableMedia med) => cache.ContainsKey(med.Meta.Title);

        public bool Contains(string title) => cache.ContainsKey(title);

        //TODO: Make this work
        public async Task<int> PruneCacheAsync(bool forceClear = false)
        {
            if (!forceClear && await GetCacheSizeAsync() < Settings.MaxFileCacheInMB * 1024 * 1024)
                return 0;

            int deletedFiles = 0;
            
            return deletedFiles;
        }

        public async Task<PlayableMedia> GetAsync(string title)
        {
            PlayableMedia media;
            try
            {
                media = await PlayableMedia.LoadFromFileAsync(cache[title]);
            }
            catch (FileNotFoundException)
            {
                cache.Remove(title, out var _);
                throw new Exception("The metadata file for this media was deleted externally... Removing...");
            }
            return media;
        }

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

                CachedMedia ca = new CachedMedia(med, localCache);
                o.Add(ca);
                cache.TryAdd(ca.Meta.Title, ca.Meta.MediaPath);
            }
            return pl ? new MediaCollection(o, col.PlaylistName, col.PlaylistIndex) : new MediaCollection(o.First());
        }
    }
}