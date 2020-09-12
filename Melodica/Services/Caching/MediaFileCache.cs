using Melodica.Services.Playback.Models;
using Melodica.Utility.Extensions;
using Discord;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using System.Diagnostics;
using Melodica.Core;
using Melodica.Services.Downloaders.Exceptions;

namespace Melodica.Services.Services
{
    public class MediaFileCache
    {
        public MediaFileCache(string dirName)
        {
            cacheLocation = Path.Combine(RootCacheLocation, dirName);
            bool exists = Directory.Exists(cacheLocation);
            if (!exists) Directory.CreateDirectory(cacheLocation);
            else LoadPreexistingFilesAsync().Wait();

            cacheInstances.Add(this);
        }

        public const int MaxClearAttempt = 5;

        public const int MaxFilesInCache = 25;

        public const string RootCacheLocation = @"./Mediacache/";

        private readonly static List<MediaFileCache> cacheInstances = new List<MediaFileCache>(); // Keep track of all instances so we can clear all cache.


        private readonly string cacheLocation;

        private readonly List<MediaMetadata> cache = new List<MediaMetadata>(MaxFilesInCache);


        public static async Task<(int deletedFiles, int filesInUse, long msDuration)> PruneAllCachesAsync()
        {
            int deletedFiles = 0, filesInUse = 0;
            long msDuration = 0;
            foreach (var cache in cacheInstances)
            {
                var result = await cache.PruneCacheAsync(true);
                deletedFiles += result.deletedFiles;
                filesInUse += result.filesInUse;
                msDuration += result.msDuration;
            }
            return (deletedFiles, filesInUse, msDuration);
        }


        private Task LoadPreexistingFilesAsync()
        {
            foreach (FileInfo metaFile in Directory.EnumerateFileSystemEntries(cacheLocation, $"*{MediaMetadata.MetaFileExtension}", SearchOption.AllDirectories).Convert(x => new FileInfo(x)))
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

        private void DeleteMediaFile(FileInfo file)
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

        public Task<long> GetCacheSizeAsync()
        {
            var files = Directory.EnumerateFiles(cacheLocation);
            if (files.Count() == 0)
                return Task.FromResult((long)0);
            return Task.FromResult(files.AsParallel().Convert(x => new FileInfo(x)).Sum(f => f.Length));
        }

        public bool Contains(string id) => cache.Any(x => x.ID == id);     

        public async Task<(int deletedFiles, int filesInUse, long msDuration)> PruneCacheAsync(bool forceClear = false)
        {
            if (!forceClear && await GetCacheSizeAsync() < BotSettings.Get().MaxFileCacheInMB * 1024 * 1024)
                return (0, 0, 0);

            int deletedFiles = 0;
            int filesInUse = 0;

            var files = Directory.EnumerateFiles(cacheLocation).Convert(x => new FileInfo(x));
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

            if (cache.Count > deletedFiles + filesInUse)
                cache.Clear();

            return (deletedFiles, filesInUse, sw.ElapsedMilliseconds);
        }

        public async Task<PlayableMedia> GetAsync(string id)
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

        public async Task<PlayableMedia> CacheMediaAsync(PlayableMedia med, bool pruneCache = true)
        {
            if (pruneCache)
                await PruneCacheAsync();

            if (!Contains(med.Info.ID ?? throw new NullReferenceException("Media ID was null.")))
            {
                cache.Add(med.Info);
                await med.SaveDataAsync(cacheLocation);
            }
            return med;
        }
    }
}