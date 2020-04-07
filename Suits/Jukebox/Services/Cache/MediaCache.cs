using Suits.Jukebox.Models;
using Suits.Utility.Extensions;
using Discord;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using System.Diagnostics;
using Suits.Core;

namespace Suits.Jukebox.Services.Cache
{
    public static class MediaCache
    {
        static MediaCache()
        {
            bool exists = Directory.Exists(CacheLocation);
            if (!exists) Directory.CreateDirectory(CacheLocation);
            else LoadPreexistingFilesAsync().Wait();
        }

        public const int MaxClearAttempt = 5;

        public const int MaxFilesInCache = 25;

        public const string CacheLocation = @"./mediacache/";

        private static readonly List<Metadata> cache = new List<Metadata>(MaxFilesInCache);

        public static event Action? OnCacheClear;

        private static async Task LoadPreexistingFilesAsync()
        {
            foreach (FileInfo metaFile in Directory.EnumerateFileSystemEntries(CacheLocation, $"*{Metadata.MetaFileExtension}", SearchOption.AllDirectories).Convert(x => new FileInfo(x)))
            {
                cache.Add(await Metadata.LoadMetadataFromFileAsync(metaFile.FullName));
            }
        }

        public static Task<long> GetCacheSizeAsync()
        {
            var files = Directory.EnumerateFiles(CacheLocation);
            if (files.Count() == 0)
                return Task.FromResult((long)0);
            return Task.FromResult(files.AsParallel().Convert(x => new FileInfo(x)).Sum(f => f.Length));
        }

        public static bool Contains(PlayableMedia med) => cache.Any(x => x.Title == med.GetTitle()); //Contains does not work correctly, so this is used instead.

        public static bool Contains(string title) => cache.Any(x => x.Title == title);

        public static async Task<(int deletedFiles, int filesInUse, long msDuration)> PruneCacheAsync(bool forceClear = false)
        {
            if (!forceClear && await GetCacheSizeAsync() < BotSettings.GetOrSet().MaxFileCacheInMB * 1024 * 1024)
                return (0, 0, 0);

            int deletedFiles = 0;
            int filesInUse = 0;
            var files = Directory.EnumerateFiles(CacheLocation).Convert(x => new FileInfo(x));
            Stopwatch sw = new Stopwatch();
            sw.Start();
            Parallel.ForEach(files, (x, s) => 
            {
                bool couldDelete = false;
                if (x.Extension == Metadata.MetaFileExtension)
                {
                    couldDelete = files.Where(y => Path.ChangeExtension(y.Name, null) == Path.ChangeExtension(x.Name, null)).Count() == 1;
                }
                if (!couldDelete)
                    try
                    {
                        x.Delete();
                        cache.Remove(cache.Single(y => y.Title == x.Name));
                        couldDelete = true;
                        deletedFiles++;
                    }
                    catch (Exception) { couldDelete = false; filesInUse++; }

                if (couldDelete)
                {
                    var mPath = Path.ChangeExtension(x.FullName, Metadata.MetaFileExtension);
                    if (!File.Exists(mPath))
                        return;
                    File.Delete(mPath);
                    deletedFiles++;
                }
            });
            sw.Stop();
            OnCacheClear?.Invoke();

            return (deletedFiles, filesInUse, sw.ElapsedMilliseconds);
        }

        public static async Task<PlayableMedia> GetAsync(string title)
        {
            PlayableMedia media;
            try
            {
                media = await PlayableMedia.LoadFromFileAsync(cache.Single(x => x.Title == title).MediaPath!);
            }
            catch (FileNotFoundException)
            {
                cache.Remove(cache.Single(x => x.Title == title));
                throw new Exception("The metadata file for this media was deleted externally... Please try again.");
            }
            return media;
        }

        public static async Task<PlayableMedia> CacheMediaAsync(PlayableMedia med, bool pruneCache = true)
        {
            if (pruneCache)
                await PruneCacheAsync();

            if (Contains(med))
                return med;

            cache.Add(med.Meta);

            return new CachedMedia(med, CacheLocation);
        }

        public static async Task<MediaCollection> CacheMediaAsync(MediaCollection col, bool pruneCache = true)
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

                CachedMedia ca = new CachedMedia(med, CacheLocation);
                o.Add(ca);
                cache.Add(ca.Meta);
            }
            return pl ? new MediaCollection(o, col.PlaylistName, col.PlaylistIndex) : new MediaCollection(o.First());
        }
    }
}