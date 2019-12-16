using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using PokerBot.Utility.Extensions;
using PokerBot.Models;

namespace PokerBot.Services.Cache
{
    public class AsyncMediaFileCache : IAsyncMediaCache
    {
        public AsyncMediaFileCache()
        {
            if (!Directory.Exists(CacheLocation))
            {
                Directory.CreateDirectory(CacheLocation);
                return;
            }

            bool clear = Core.Settings.ClearFileCacheOnStartup;
            foreach (string file in Directory.EnumerateFiles(CacheLocation))
            {
                if (clear)
                    File.Delete(file);

                var info = new FileInfo(file);
                if (!cache.TryAdd(info.Name, new PlayableMedia(info.Name, info.FullName, info.Extension)))
                    throw new Exception("Could not add pre-existing file to cache.");
            }
        }

        public const string CacheLocation = @"./mediacache/";

        public static int MaxCache { get; private set; } = 25;

        private readonly ConcurrentDictionary<string, PlayableMedia> cache = 
            new ConcurrentDictionary<string, PlayableMedia>(Environment.ProcessorCount * 2, MaxCache);

        private Task CheckCacheSizeAsync()
        {
            long sum = 0;
            foreach (var file in Directory.EnumerateFiles(CacheLocation))
                sum += new FileInfo(file).Length;

            if (sum > Core.Settings.MaxFileCacheInMB * 1024 * 1024)
                ClearCache();
            return Task.CompletedTask;
        }

        public void ClearCache()
        {
            foreach (var file in Directory.EnumerateFiles(CacheLocation))
                File.Delete(file);
            cache.Clear();
        }

        public bool ExistsInCache(string key) => cache.ContainsKey(key.RemoveSpecialCharacters());

        public Task<PlayableMedia> GetValueAsync(string key) =>
            Task.FromResult(cache.GetValueOrDefault(key.RemoveSpecialCharacters()));

        public async Task<PlayableMedia> CacheAsync(PlayableMedia media, bool checkCacheSize = true)
        {
            var cacheName = media.Name.RemoveSpecialCharacters();

            if (ExistsInCache(cacheName))
                return cache[cacheName];

            if (checkCacheSize)
                await CheckCacheSizeAsync();
           
            var path = Path.ChangeExtension(Path.Combine(CacheLocation, cacheName), media.Format);          

            using var file = File.OpenWrite(path);

            await media.Stream.CopyToAsync(file);
            file.Close();

            var output = new PlayableMedia(media.Name, path, media.Format);

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
