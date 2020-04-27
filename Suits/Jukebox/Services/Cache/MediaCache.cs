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

        private static Task LoadPreexistingFilesAsync()
        {
            foreach (FileInfo metaFile in Directory.EnumerateFileSystemEntries(CacheLocation, $"*{Metadata.MetaFileExtension}", SearchOption.AllDirectories).Convert(x => new FileInfo(x)))
            {
                cache.Add(Metadata.LoadFromFile(metaFile.FullName));
            }
            return Task.CompletedTask;
        }

        public static Task<long> GetCacheSizeAsync()
        {
            var files = Directory.EnumerateFiles(CacheLocation);
            if (files.Count() == 0)
                return Task.FromResult((long)0);
            return Task.FromResult(files.AsParallel().Convert(x => new FileInfo(x)).Sum(f => f.Length));
        }

        public static bool Contains(string id) => cache.Any(x => x.ID == id);

        public static async Task<(int deletedFiles, int filesInUse, long msDuration)> PruneCacheAsync(bool forceClear = false)
        {
            if (!forceClear && await GetCacheSizeAsync() < BotSettings.Get().MaxFileCacheInMB * 1024 * 1024)
                return (0, 0, 0);

            int deletedFiles = 0;
            int filesInUse = 0;
            var files = Directory.EnumerateFiles(CacheLocation).Convert(x => new FileInfo(x));
            Stopwatch sw = new Stopwatch();
            sw.Start();
            Parallel.ForEach<FileInfo>(files, (file, loop) =>
            {
                if (file.Extension == Metadata.MetaFileExtension)
                    return;
                try
                {
                    file.Delete();
                    File.Delete(Path.ChangeExtension(file.FullName, Metadata.MetaFileExtension));
                    cache.Remove(cache.Single(x => x.ID == Path.ChangeExtension(file.Name, null)));
                    deletedFiles++;
                }
                catch
                {
                    filesInUse++;
                }
            });
            sw.Stop();
            OnCacheClear?.Invoke();

            return (deletedFiles, filesInUse, sw.ElapsedMilliseconds);
        }

        public static async Task<PlayableMedia> GetAsync(string id)
        {
            PlayableMedia media;
            try
            {
                media = await PlayableMedia.LoadFromFileAsync(cache.Single(x => x.ID == id).MediaPath!);
            }
            catch (FileNotFoundException)
            {
                cache.Clear();
                await LoadPreexistingFilesAsync();
                throw new Exception("The metadata file for this media was deleted externally... Please try again.");
            }
            return media;
        }

        public static async Task<CachedMedia> CacheMediaAsync(PlayableMedia med, bool pruneCache = true)
        {
            if (pruneCache)
                await PruneCacheAsync();

            if (Contains(med.Info.ID ?? throw new NullReferenceException("Medias ID was null.")))
                return (CachedMedia)med;

            cache.Add(med.Info);

            return new CachedMedia(med, CacheLocation);
        }

        public static async Task<MediaCollection> CacheMediaAsync(MediaCollection col, bool pruneCache = true)
        {
            if (pruneCache)
                await PruneCacheAsync();

            var pl = col.IsPlaylist;
            var playlist = new List<PlayableMedia>();
            foreach (var med in col)
            {
                var medID = med.Info.ID ?? throw new NullReferenceException("Medias ID was null.");
                if (Contains(medID))
                {
                    playlist.Add(await GetAsync(medID));
                    continue;
                }

                CachedMedia ca = new CachedMedia(med, CacheLocation);
                playlist.Add(ca);
                cache.Add(ca.Info);
            }
            return pl ? new MediaCollection(playlist, col.Info, col.PlaylistIndex) : new MediaCollection(playlist.First());
        }
    }
}