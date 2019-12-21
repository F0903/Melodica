using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using PokerBot.Modules.Jukebox.Models;
using PokerBot.Utility.Extensions;

namespace PokerBot.Modules.Jukebox.Services.Cache
{
    public class AsyncMediaFileCache : IAsyncMediaCache
    {
        public AsyncMediaFileCache(Discord.IGuild guild)
        {
            cacheLocation = Path.Combine(CacheRoot, guild.Name.ReplaceIllegalCharacters());

            if (!Directory.Exists(cacheLocation))
            {
                Directory.CreateDirectory(cacheLocation);
                return;
            }

            bool clear = Settings.ClearFileCacheOnStartup;
            Parallel.ForEach(Directory.EnumerateFiles(cacheLocation), x => 
            {
                if (clear)
                    File.Delete(x);

                var info = new FileInfo(x);
                if (!cache.TryAdd(info.Name, new PlayableMedia(info.Name, info.FullName, info.Extension, 0)))
                    throw new Exception("Could not add pre-existing file to cache.");
            });
        }

        public const string CacheRoot = @"./mediacache/";

        private readonly string cacheLocation;

        public static int MaxCache { get; private set; } = 25;

        private readonly ConcurrentDictionary<string, PlayableMedia> cache =
            new ConcurrentDictionary<string, PlayableMedia>(Environment.ProcessorCount * 2, MaxCache);

        private Task CheckCacheSizeAsync()
        {
            long sum = Directory.EnumerateFiles(cacheLocation).Sum(x => x.Length);

            if (sum > PokerBot.Settings.MaxFileCacheInMB * 1024 * 1024)
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

        public async Task<PlayableMedia> CacheAsync(PlayableMedia media, bool checkCacheSize = true)
        {
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
