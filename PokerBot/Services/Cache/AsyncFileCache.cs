using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using PokerBot.Utility.Extensions;

namespace PokerBot.Services.Cache
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
                if (!cache.TryAdd(info.Name, (info.FullName, info.Extension)))
                    throw new Exception("Could not add pre-existing file to cache.");
            }
        }

        public const string CacheLocation = @"./mediacache/";

        public static int MaxCache { get; private set; } = 25;

        private readonly ConcurrentDictionary<string, (string path, string format)> cache = 
            new ConcurrentDictionary<string, (string path, string format)>(Environment.ProcessorCount * 2, MaxCache);

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

        public Task<(string path, string format)> GetValueAsync(string key) =>
            Task.FromResult(cache.GetValueOrDefault(key.RemoveSpecialCharacters()));

        public async Task<(string path, string format)> CacheAsync(Stream val, string cacheName, string format, bool checkCacheSize = true)
        {
            cacheName = cacheName.RemoveSpecialCharacters();

            if (ExistsInCache(cacheName))
                return cache[cacheName];

            if (checkCacheSize)
                await CheckCacheSizeAsync();
           
            var path = Path.ChangeExtension(Path.Combine(CacheLocation, cacheName), format);          

            using var file = File.OpenWrite(path);

            await val.CopyToAsync(file);
            file.Close();

            var output = (path, format);

            cache.TryAdd(cacheName, output);

            return output;
        }

        public Task<byte[]> GetCacheAsync(string name)
        {
            throw new NotImplementedException();
        }
    }
}
