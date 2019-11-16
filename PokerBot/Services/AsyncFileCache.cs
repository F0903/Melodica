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
    public class AsyncFileCache : IAsyncCachingService
    {
        public AsyncFileCache()
        {
            if (!Directory.Exists(CacheLocation))
                return;

            foreach (string file in Directory.EnumerateFiles(CacheLocation))
            {
                var info = new FileInfo(file);
                if (!cache.TryAdd(info.Name, info.FullName))
                    throw new Exception("Could not add pre-existing file to cache.");
            }
        }

        public const string CacheLocation = @"./mediacache/";

        public static int MaxCache { get; private set; } = 25;

        private readonly ConcurrentDictionary<string, string> cache = new ConcurrentDictionary<string, string>(5, MaxCache);

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

        public async Task<string> CacheAsync((Stream stream, string name) result)
        {
            await CheckCacheSizeAsync();

            result.name = result.name.RemoveSpecialCharacters();

            if (!Directory.Exists(CacheLocation))
                Directory.CreateDirectory(CacheLocation);
            var path = Path.Combine(CacheLocation, result.name);

            using var file = File.OpenWrite(path);

            await result.stream.CopyToAsync(file);
            if (cache.TryAdd(result.name, path))
                return path;
            return null;
        }

        public Task<byte[]> GetCacheAsync(string name)
        {
            throw new NotImplementedException();
        }
    }
}
