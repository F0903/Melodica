using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Melodica.Core;
using Melodica.Services.Downloaders.Exceptions;
using Melodica.Services.Media;
using Melodica.Utility.Extensions;

namespace Melodica.Services.Caching
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

        private static readonly List<MediaFileCache> cacheInstances = new List<MediaFileCache>(); // Keep track of all instances so we can clear all cache.

        public const int MaxClearAttempt = 5;

        public const int MaxFilesInCache = 25;

        public const string RootCacheLocation = @"./Mediacache/";

        private readonly string cacheLocation;

        private readonly Dictionary<string, (MediaMetadata media, long accessCount)> cache = new Dictionary<string, (MediaMetadata media, long accessCount)>(MaxFilesInCache);

        public static async Task<(int deletedFiles, int filesInUse, long msDuration)> ClearAllCachesAsync()
        {
            if (cacheInstances.Count == 0)
                throw new Exception("No cache instances have been instanciated. Please play a song first to create the caches.");
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
            foreach (var metaFile in Directory.EnumerateFileSystemEntries(cacheLocation, $"*{MediaMetadata.MetaFileExtension}", SearchOption.AllDirectories).Convert(x => new FileInfo(x)))
            {
                try
                {
                    var med = MediaMetadata.LoadFromFile(metaFile.FullName);
                    cache.Add(med.Id ?? throw new Exception("Id was null."), (med, 0));
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
                if (file.DirectoryName != null)
                    foreach (string? dirFile in Directory.EnumerateFiles(file.DirectoryName, $"{Path.ChangeExtension(file.Name, null)}.*"))
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
            if (files.Any())
                return Task.FromResult((long)0);
            return Task.FromResult(files.AsParallel().Convert(x => new FileInfo(x)).Sum(f => f.Length));
        }

        public bool Contains(string id) => cache.ContainsKey(id);

        private (int deletedFiles, int filesInUse, long msDuration) NukeCache()
        {
            int deletedFiles = 0;
            int filesInUse = 0;

            var files = Directory.EnumerateFiles(cacheLocation).Convert(x => new FileInfo(x));
            var sw = new Stopwatch();
            sw.Start();
            Parallel.ForEach<FileInfo>(files, (file, loop) =>
            {
                if (file.Extension == MediaMetadata.MetaFileExtension)
                    return;
                try
                {
                    DeleteMediaFile(file);
                    var name = Path.ChangeExtension(file.Name, null);
                    var (media, accessCount) = cache[name];
                    if (media != null) cache.Remove(name);
                    ++deletedFiles;
                }
                catch
                {
                    ++filesInUse;
                }
            });
            sw.Stop();
            return (deletedFiles, filesInUse, sw.ElapsedMilliseconds);
        }

        private (int deletedFiles, int filesInUse, long msDuration) PruneCache(bool force)
        {
            var maxSize = BotSettings.CacheSizeMB;
            if (!force && cache.Count < maxSize)
                return (0, 0, 0);

            int deletedFiles = 0;
            int filesInUse = 0;

            var sw = new Stopwatch();
            sw.Start();

            var ordered = cache.OrderByDescending(x => x.Value.accessCount);
            Parallel.ForEach(ordered, (x, s, i) =>
            {
                if (!force && i > cache.Count - maxSize)
                {
                    s.Stop();
                    return;
                }

                try
                {
                    DeleteMediaFile(new FileInfo(x.Value.media.DataInformation.MediaPath ?? throw new Exception("MediaPath was null when trying to delete media file in PruneCache.")));
                    cache.Remove(x.Key);
                    ++deletedFiles;
                }
                catch { ++filesInUse; }
            });

            sw.Stop();
            return (deletedFiles, filesInUse, sw.ElapsedMilliseconds);
        }

        public async Task<(int deletedFiles, int filesInUse, long msDuration)> PruneCacheAsync(bool forceClear = false, bool nuke = false)
        {
            if (!forceClear && await GetCacheSizeAsync() < BotSettings.CacheSizeMB * 1024 * 1024)
                return (0, 0, 0);

            var (deletedFiles, filesInUse, msDuration) = nuke ? NukeCache() : PruneCache(forceClear);

            return (deletedFiles, filesInUse, msDuration);
        }

        public Task<PlayableMedia> GetAsync(string id)
        {
            Task<PlayableMedia> mediaTask;
            try
            {
                var (media, accessCount) = cache[id];
                cache[id] = (media, accessCount + 1); // This looks grim
                mediaTask = PlayableMedia.LoadFromFileAsync(media.DataInformation.MediaPath!);
            }
            catch (FileNotFoundException)
            {
                cache.Clear();
                LoadPreexistingFilesAsync();
                throw new MissingMetadataException("The metadata file for this media was deleted externally... Please try again.");
            }
            return mediaTask;
        }

        public async Task<PlayableMedia> CacheMediaAsync(PlayableMedia med, bool pruneCache = true)
        {
            if (pruneCache)
                await PruneCacheAsync();

            if (!Contains(med.Info.Id ?? throw new NullReferenceException("Media ID was null.")))
            {
                cache.Add(med.Info.Id, (med.Info, 1));
                await med.SaveDataAsync(cacheLocation);
            }
            return med;
        }
    }
}