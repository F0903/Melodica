using CasinoBot.Modules.Jukebox.Models;
using CasinoBot.Utility.Extensions;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace CasinoBot.Modules.Jukebox.Services.Cache
{
    public class AsyncMediaFileCache : IAsyncMediaCache
    {           
        public static int MaxCache { get; private set; } = 25;

        public const string CacheRoot = @"./mediacache/";

        private string cacheLocation;

        private bool init = false;

        private readonly ConcurrentDictionary<string, PlayableMedia> cache =
            new ConcurrentDictionary<string, PlayableMedia>(Environment.ProcessorCount * 2, MaxCache);

        private Task CheckCacheSizeAsync()
        {
            if (cacheLocation == null)
                throw new Exception("Cache location has not been set.");

            long sum = Directory.EnumerateFiles(cacheLocation).Sum(x => x.Length);

            if (sum > Settings.MaxFileCacheInMB * 1024 * 1024)
                ClearCache();
            return Task.CompletedTask;
        }

        public void ClearCache()
        {
            Parallel.ForEach(Directory.EnumerateFiles(cacheLocation), x => File.Delete(x));
            cache.Clear();
        }

        public bool ExistsInCache(string key) => cache.ContainsKey(key.ReplaceIllegalCharacters());

        public Task<PlayableMedia> GetValueAsync(string key) =>
            Task.FromResult(cache.GetValueOrDefault(key.ReplaceIllegalCharacters()));

        public Task Init(string guildName)
        {
            if (init)
                return Task.CompletedTask;

            cacheLocation = Path.Combine(CacheRoot, guildName);
            ClearCache();
            init = true;
            return Task.CompletedTask;
        }

        public async Task<PlayableMedia> CacheAsync(PlayableMedia media, bool checkCacheSize = true)
        {
            if (!init)
                throw new Exception("You need to initialize the file cache before using it.");

            var cacheName = media.Title.ReplaceIllegalCharacters();

            if (ExistsInCache(cacheName))
                return cache[cacheName];

            if (checkCacheSize)
                await CheckCacheSizeAsync();

            var path = Path.ChangeExtension(Path.Combine(cacheLocation, cacheName), media.Format);

            FileStream file = null;
            try
            {
                file = File.OpenWrite(path);
                await media.Stream.CopyToAsync(file);
            }
            catch (Exception) { return null; }
            finally
            {
                file.Close();
            }

            var output = new PlayableMedia(media.Title, path, media.Format, media.SecondDuration);

            if (!cache.TryAdd(cacheName, output))
                throw new Exception("Unable to add media to cache.");

            return output;
        }

        public Task<byte[]> GetCacheAsync(string name)
        {
            throw new NotImplementedException();
        }
    }
}