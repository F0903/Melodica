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
using Suits.Jukebox.Models.Exceptions;

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

        public const string CacheLocation = @"./Mediacache/";

        private static readonly List<MediaMetadata> cache = new List<MediaMetadata>(MaxFilesInCache);

        public static event Action? OnCacheClear;

        private static Task LoadPreexistingFilesAsync()
        {
            foreach (FileInfo metaFile in Directory.EnumerateFileSystemEntries(CacheLocation, $"*{MediaMetadata.MetaFileExtension}", SearchOption.AllDirectories).Convert(x => new FileInfo(x)))
            {
                try
                {
                    cache.Add(MediaMetadata.LoadFromFile(metaFile.FullName));
                }
                catch (Exception)
                {
                    DeleteMediaFile(metaFile);
                }
            }
            return Task.CompletedTask;
        }

        private static void DeleteMediaFile(FileInfo file)
        {
            // If the file specified is a metadata file.
            if (file.Extension == MediaMetadata.MetaFileExtension)
            {
                file.Delete();
                foreach (var dirFile in Directory.EnumerateFiles(file.DirectoryName, $"{Path.ChangeExtension(file.Name, null)}.*"))
                {
                    File.Delete(dirFile);
                }
            }

            file.Delete();
            File.Delete(Path.ChangeExtension(file.FullName, MediaMetadata.MetaFileExtension));
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
                if (file.Extension == MediaMetadata.MetaFileExtension)
                    return;
                try
                {
                    DeleteMediaFile(file);
                    var cacheElement = cache.SingleOrDefault(x => x.ID == Path.ChangeExtension(file.Name, null));
                    if(cacheElement != null) cache.Remove(cacheElement);
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
                media = await PlayableMedia.LoadFromFileAsync(cache.Single(x => x.ID == id).DataInformation.MediaPath!);
            }
            catch (FileNotFoundException)
            {
                cache.Clear();
                await LoadPreexistingFilesAsync();
                throw new MissingMetadataException("The metadata file for this media was deleted externally... Please try again.");
            }
            return media;
        }

        public static async Task<CachedMedia> CacheMediaAsync(PlayableMedia med, bool pruneCache = true)
        {
            if (pruneCache)
                await PruneCacheAsync();

            if (Contains(med.Info.ID ?? throw new NullReferenceException("Media ID was null.")))
                return (CachedMedia)med;

            cache.Add(med.Info);

            return new CachedMedia(med, CacheLocation);
        }
    }
}