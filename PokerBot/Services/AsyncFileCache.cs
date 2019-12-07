using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using PokerBot.Utility.Extensions;

namespace PokerBot.Services
{
    public class AsyncFileCache : IAsyncCache<Stream>
    {
        public AsyncFileCache()
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
                if (!cache.TryAdd(info.Name, info.FullName))
                    throw new Exception("Could not add pre-existing file to cache.");
            }
        }

        public const string CacheLocation = @"./mediacache/";

        public static int MaxCache { get; private set; } = 25;

        private readonly ConcurrentDictionary<string, string> cache = new ConcurrentDictionary<string, string>(Environment.ProcessorCount * 2, MaxCache);

        private Task CheckCacheSizeAsync()
        {
            long sum = 0;
            foreach (var file in Directory.EnumerateFiles(CacheLocation))
                sum += new FileInfo(file).Length;

            if (sum > Core.Settings.MaxFileCacheInGB * 1024 * 1024 * 1024)
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

        public Task<string> GetValueAsync(string key) =>
            Task.FromResult(cache.GetValueOrDefault(key.RemoveSpecialCharacters()));

        public async Task<string> CacheAsync(Stream val, string cacheName, bool checkCacheSize = true)
        {
            if (checkCacheSize)
                await CheckCacheSizeAsync();

            cacheName = cacheName.RemoveSpecialCharacters();

            var path = Path.Combine(CacheLocation, cacheName);

            if (File.Exists(path))
                return path;

            using var file = File.OpenWrite(path);

            await val.CopyToAsync(file);
            file.Close();

            cache.TryAdd(cacheName, path);

            return path;
        }

        public Task<byte[]> GetCacheAsync(string name)
        {
            throw new NotImplementedException();
        }
    }
}
