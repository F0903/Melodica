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

        record CachePair(PlayableMedia Media, long AccessCount);

        private static readonly List<MediaFileCache> cacheInstances = new(); // Keep track of all instances so we can clear all cache.

        public const int MaxClearAttempt = 5;

        public const string RootCacheLocation = @"./Mediacache/";

        private readonly string cacheLocation;

        private readonly Dictionary<string, CachePair> cache = new();

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

        private async Task LoadPreexistingFilesAsync()
        {
            foreach (var metaFile in Directory.EnumerateFileSystemEntries(cacheLocation, $"*{MediaInfo.MetaFileExtension}", SearchOption.AllDirectories).Convert(x => new FileInfo(x)))
            {
                try
                {
                    var info = await MediaInfo.LoadFromFile(metaFile.FullName);
                    var med = await PlayableMedia.FromExistingInfo(info);
                    var id = info.Id ?? throw new Exception("Id was null.");
                    cache.Add(id, new(med, 0));
                }
                catch (Exception)
                {
                    DeleteMediaFile(metaFile);
                }
            }
        }

        private static void DeleteMediaFile(FileInfo file)
        {
            // If the file specified is a metadata file.
            if (file.Extension == MediaInfo.MetaFileExtension)
            {
                file.Delete();
                if (file.DirectoryName != null)
                {
                    foreach (string? dirFile in Directory.EnumerateFiles(file.DirectoryName, $"{Path.ChangeExtension(file.Name, null)}.*"))
                    {
                        File.Delete(dirFile);
                    }
                }
            }

            file.Delete();
            File.Delete(Path.ChangeExtension(file.FullName, MediaInfo.MetaFileExtension));
        }

        public Task<long> GetCacheSizeAsync()
        {
            var files = Directory.EnumerateFiles(cacheLocation);
            return Task.FromResult(files.AsParallel().Convert(x => new FileInfo(x)).Sum(f => f.Length));
        }

        private (int deletedFiles, int filesInUse, long msDuration) NukeCache()
        {
            int deletedFiles = 0;
            int filesInUse = 0;

            var files = Directory.EnumerateFiles(cacheLocation).Convert(x => new FileInfo(x));
            var sw = new Stopwatch();
            sw.Start();
            Parallel.ForEach<FileInfo>(files, (file, loop) =>
            {
                if (file.Extension == MediaInfo.MetaFileExtension)
                    return;
                try
                {
                    DeleteMediaFile(file);
                    var name = Path.ChangeExtension(file.Name, null);
                    var (media, accessCount) = cache[name];
                    if (media != null)
                        cache.Remove(name);
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

            var ordered = cache.OrderByDescending(x => x.Value.AccessCount);
            Parallel.ForEach(ordered, (x, s, i) =>
            {
                if (!force && i > cache.Count - maxSize)
                {
                    s.Stop();
                    return;
                }

                try
                {
                    var pair = x.Value;
                    var media = pair.Media;
                    var dataInfo = media.DataInfo;
                    var file = dataInfo.MediaPath ?? throw new Exception("MediaPath or DataInfo was null when trying to delete media file in PruneCache.");
                    DeleteMediaFile(new FileInfo(file));
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

        async Task<PlayableMedia> InternalGetAsync(string id)
        {
            var pair = cache[id];
            cache[id] = pair with { AccessCount = pair.AccessCount + 1 };
            return await PlayableMedia.FromExistingInfo(pair.Media.Info);
        }

        public async Task<PlayableMedia> GetAsync(string id)
        {
            try
            {
                return await InternalGetAsync(id);
            }
            catch (FileNotFoundException)
            {
                cache.Clear();
                await LoadPreexistingFilesAsync();
                throw new MissingMetadataException("The metadata file for this media was deleted externally... Please try again.");
            }
        }

        public bool TryGetAsync(string id, out PlayableMedia? media)
        {
            try
            {
                media = InternalGetAsync(id).GetAwaiter().GetResult();
                return true;
            }
            catch
            {
                media = null;
                return false;
            }
        }

        public async Task<string> CacheAsync(PlayableMedia med, bool pruneCache = true)
        {
            if (pruneCache)
                await PruneCacheAsync();

            var info = med.Info;
            var id = info.Id ?? throw new NullReferenceException("Media ID was null.");
            try { cache.Add(id, new(med, 1)); }
            catch (ArgumentException) { }
            var savedPath = await med.SaveDataAsync(cacheLocation);
            return savedPath;
        }
    }
}