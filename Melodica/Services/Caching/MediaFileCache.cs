using System;
using System.Diagnostics;
using Melodica.Config;
using Melodica.Services.Downloaders.Exceptions;
using Melodica.Services.Media;
using Melodica.Services.Serialization;
using Melodica.Utility;

namespace Melodica.Services.Caching;

public sealed class MediaFileCache : IMediaCache
{
    public MediaFileCache(string dirName)
    {
        var root = Path.Combine(Directory.GetCurrentDirectory(), "mediacache");
        cacheLocation = Path.Combine(root, dirName);
        var exists = Directory.Exists(cacheLocation);
        if (!exists) Directory.CreateDirectory(cacheLocation);
        else LoadPreexistingFilesAsync().Wait();

        cacheInstances.Add(this);
    }

    record struct CacheInfo(CachedMediaInfo CachedMediaInfo, long AccessCount);

    private static readonly List<MediaFileCache> cacheInstances = []; // Keep track of all instances so we can clear all cache.

    public const int MaxClearAttempt = 5;

    private readonly string cacheLocation;

    private readonly Dictionary<string, CacheInfo> cache = [];

    public static async Task<(int deletedFiles, int filesInUse, long msDuration)> ClearAllCachesAsync()
    {
        if (cacheInstances.Count == 0)
            return (0, 0, 0);
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
        foreach (var metaFile in Directory.EnumerateFileSystemEntries(cacheLocation, $"*{CachedMediaInfo.MetaFileExtension}", SearchOption.AllDirectories).Convert(x => new FileInfo(x)))
        {
            try
            {
                var info = await CachedMediaInfo.LoadFromDisk(metaFile.FullName);
                var id = info.Id ?? throw new Exception("Id was null.");
                cache.Add(id, new(info, 0));
            }
            catch
            {
                DeleteMedia(metaFile);
            }
        }
    }

    static bool DeleteMedia(FileInfo file)
    {
        try
        {
            // Always try to delete the media file before the meta to know if it's in use.
            if (file.Extension == CachedMediaInfo.MetaFileExtension)
            {
                File.Delete(Path.ChangeExtension(file.FullName, null));
                file.Delete();
            }

            file.Delete();
            File.Delete(Path.ChangeExtension(file.FullName, CachedMediaInfo.MetaFileExtension));
            return true;
        }
        catch
        {
            return false;
        }
    }

    public Task<long> GetCacheSizeAsync()
    {
        var files = Directory.EnumerateFiles(cacheLocation);
        return files.AsParallel().Convert(x => new FileInfo(x)).Sum(f => f.Length).WrapTask();
    }

    private Task<(int deletedFiles, int filesInUse, long msDuration)> NukeCacheAsync()
    {
        var deletedFiles = 0;
        var filesInUse = 0;

        var files = Directory.EnumerateFiles(cacheLocation).Convert(x => new FileInfo(x));
        Stopwatch sw = new();
        sw.Start();
        Parallel.ForEach<FileInfo>(files, (file, loop) =>
        {
            if (file.Extension == CachedMediaInfo.MetaFileExtension)
                return;

            var name = Path.ChangeExtension(file.Name, null);
            (var cachedInfo, var accessCount) = cache[name];

            if (!DeleteMedia(file))
            {
                ++filesInUse;
                return;
            }

            cache.Remove(name);
            ++deletedFiles;
        });
        sw.Stop();
        return (deletedFiles, filesInUse, sw.ElapsedMilliseconds).WrapTask();
    }

    private Task<(int deletedFiles, int filesInUse, long msDuration)> PruneCacheAsync(bool force)
    {
        var maxSize = BotConfig.Settings.CacheSizeMB;
        if (!force && cache.Count < maxSize)
            return (0, 0, 0L).WrapTask();

        var deletedFiles = 0;
        var filesInUse = 0;

        Stopwatch sw = new();
        sw.Start();

        var ordered = cache.OrderByDescending(x => x.Value.AccessCount);
        Parallel.ForEach(ordered, (x, s, i) =>
        {
            if (!force && i > cache.Count - maxSize)
            {
                s.Stop();
                return;
            }


            var cacheInfo = x.Value;

            var file = cacheInfo.CachedMediaInfo.MediaPath ?? throw new Exception("MediaPath or DataInfo was null when trying to delete media file in PruneCache.");
            if (!DeleteMedia(new(file)))
            {
                ++filesInUse;
                return;
            }

            cache.Remove(x.Key);
            ++deletedFiles;
        });

        sw.Stop();
        return (deletedFiles, filesInUse, sw.ElapsedMilliseconds).WrapTask();
    }

    public async Task<(int deletedFiles, int filesInUse, long msDuration)> PruneCacheAsync(bool forceClear = false, bool nuke = false)
    {
        if (!forceClear && await GetCacheSizeAsync() < BotConfig.Settings.CacheSizeMB * 1024 * 1024)
            return (0, 0, 0);

        (var deletedFiles, var filesInUse, var msDuration) = nuke ? await NukeCacheAsync() : await PruneCacheAsync(forceClear);

        return (deletedFiles, filesInUse, msDuration);
    }

    public ValueTask<MediaInfo?> TryGetInfoAsync(string id)
    {
        if (cache.TryGetValue(id, out var cacheInfo))
        {
            var mediaInfo = cacheInfo.CachedMediaInfo;
            cache[id] = cacheInfo with { AccessCount = cacheInfo.AccessCount + 1 };
            return mediaInfo.WrapValueTask<MediaInfo?>();
        }

        return ValueTask.FromResult<MediaInfo?>(null);
    }

    public ValueTask<PlayableMediaStream?> TryGetAsync(string id)
    {
        if (cache.TryGetValue(id, out var cacheInfo))
        {
            var mediaInfo = cacheInfo.CachedMediaInfo;

            if (!mediaInfo.IsMediaComplete)
            {
                try
                {
                    File.Delete(mediaInfo.MediaPath);
                    File.Delete(Path.ChangeExtension(mediaInfo.MediaPath, CachedMediaInfo.MetaFileExtension));
                }
                catch { }
                return default;
            }

            cache[id] = cacheInfo with { AccessCount = cacheInfo.AccessCount + 1 };
            var media = new PlayableMediaStream(File.OpenRead(mediaInfo.MediaPath), mediaInfo, null, null);
            return media.WrapValueTask<PlayableMediaStream?>();
        }

        return default;
    }

    public async ValueTask TryEditCacheInfo(string id, Func<CachedMediaInfo, CachedMediaInfo> modifier)
    {
        if (cache.TryGetValue(id, out var info))
        {
            var modified = modifier(info.CachedMediaInfo);
            cache[id] = info with { CachedMediaInfo = modified };
            await modified.WriteToDisk();
        }
    }

    public async ValueTask<Stream> InitStreamableCache(MediaInfo info, bool pruneCache = true)
    {
        if (pruneCache) await PruneCacheAsync();

        var id = info.Id;

        var fileLegalId = id.ReplaceIllegalCharacters();
        var mediaLocation = Path.Combine(cacheLocation, fileLegalId);
        var file = File.OpenWrite(mediaLocation);

        var cachedMediaInfo = new CachedMediaInfo(mediaLocation, cacheLocation, info)
        {
            IsMediaComplete = false,
        };
        await cachedMediaInfo.WriteToDisk();

        try { cache.Add(id, new(cachedMediaInfo, 0)); }
        catch (ArgumentException) { }

        return file;
    }
}
