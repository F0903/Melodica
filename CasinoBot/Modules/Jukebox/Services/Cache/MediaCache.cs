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
            if (!Directory.Exists(cacheLocation))
                Directory.CreateDirectory(cacheLocation);
        }

        public const int MaxFilesInCache = 25;

        public const string CacheRoot = @"./mediacache/";

        private readonly List<PlayableMedia> cache = new List<PlayableMedia>();

        private readonly string cacheLocation;

        public Task<long> GetCacheSizeAsync() =>
           Task.FromResult(Directory.EnumerateFiles(cacheLocation).AsParallel().Convert(x => new FileInfo(x)).Sum(f => f.Length));

        public bool ValExistsInCache(PlayableMedia med) => cache.Contains(med);

        public async Task<bool> PruneCacheAsync()
        {
            if (await GetCacheSizeAsync() < Settings.MaxFileCacheInMB * 1024 * 1024)
                return false;

            cache.Clear();
            Parallel.ForEach(Directory.EnumerateFiles(cacheLocation).Convert(x => new FileInfo(x)), f => f.Delete());
            return true;
        }

        public async Task<MediaCollection> CacheMediaAsync(MediaCollection col, bool pruneCache = true)
        {
            if (pruneCache)
                await PruneCacheAsync();

            var pl = col.IsPlaylist;

            var o = new List<PlayableMedia>(); 
            foreach (var med in col)
            {
                if (ValExistsInCache(med))
                {
                    o.Add(cache.Single(x => x == med));
                    continue;
                }
                var fPath = Path.Combine(cacheLocation, $"{med.Title.ReplaceIllegalCharacters()}.{med.Format}");
                using var fs = File.Create(fPath);
                await med.Stream.CopyToAsync(fs);
                fs.Close();
                o.Add(new PlayableMedia(med.Title, fPath, med.Format, med.SecondDuration));
            }
            return pl ? new MediaCollection(o, col.PlaylistName, col.PlaylistIndex) : new MediaCollection(o.First());
        }
    }
}