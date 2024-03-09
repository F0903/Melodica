using System;
using System.Diagnostics;
using Melodica.Config;
using Melodica.Services.Downloaders.Exceptions;
using Melodica.Services.Media;
using Melodica.Services.Serialization;
using Melodica.Utility;

namespace Melodica.Services.Caching;

public sealed class NoMediaFileCachesException : Exception
{
    public NoMediaFileCachesException() : base("No cache instances have been instanciated. Please play a song first to create the caches.") { }
}

public sealed class MediaFileCache
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
            throw new NoMediaFileCachesException();
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
            catch (Exception)
            {
                DeleteMediaFile(metaFile);
            }
        }
    }

    private static void DeleteMediaFile(FileInfo file)
    {
        // If the file specified is a metadata file.
        if (file.Extension == CachedMediaInfo.MetaFileExtension)
        {
            file.Delete();
            if (file.DirectoryName != null)
            {
                foreach (var dirFile in Directory.EnumerateFiles(file.DirectoryName, $"{Path.ChangeExtension(file.Name, null)}.*"))
                {
                    File.Delete(dirFile);
                }
            }
        }

        file.Delete();
        File.Delete(Path.ChangeExtension(file.FullName, CachedMediaInfo.MetaFileExtension));
    }

    public Task<long> GetCacheSizeAsync()
    {
        var files = Directory.EnumerateFiles(cacheLocation);
        return Task.FromResult(files.AsParallel().Convert(x => new FileInfo(x)).Sum(f => f.Length));
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
            try
            {
                var name = Path.ChangeExtension(file.Name, null);
                (var cachedInfo, var accessCount) = cache[name];

                if (cachedInfo.IsMediaInUse)
                {
                    ++filesInUse;
                    return;
                }

                DeleteMediaFile(file);
                cache.Remove(name);
                ++deletedFiles;
            }
            catch
            {
                ++filesInUse;
            }
        });
        sw.Stop();
        return Task.FromResult((deletedFiles, filesInUse, sw.ElapsedMilliseconds));
    }

    private Task<(int deletedFiles, int filesInUse, long msDuration)> PruneCacheAsync(bool force)
    {
        var maxSize = BotConfig.Settings.CacheSizeMB;
        if (!force && cache.Count < maxSize)
            return Task.FromResult((0, 0, 0L));

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

            try
            {
                var cacheInfo = x.Value;
                
                if (cacheInfo.CachedMediaInfo.IsMediaInUse)
                {
                    ++filesInUse;
                    return;
                }

                var file = cacheInfo.CachedMediaInfo.MediaFilePath ?? throw new Exception("MediaPath or DataInfo was null when trying to delete media file in PruneCache.");
                DeleteMediaFile(new(file));
                cache.Remove(x.Key);
                ++deletedFiles;
            }
            catch { ++filesInUse; }
        });

        sw.Stop();
        return Task.FromResult((deletedFiles, filesInUse, sw.ElapsedMilliseconds));
    }

    public async Task<(int deletedFiles, int filesInUse, long msDuration)> PruneCacheAsync(bool forceClear = false, bool nuke = false)
    {
        if (!forceClear && await GetCacheSizeAsync() < BotConfig.Settings.CacheSizeMB * 1024 * 1024)
            return (0, 0, 0);

        (var deletedFiles, var filesInUse, var msDuration) = nuke ? await NukeCacheAsync() : await PruneCacheAsync(forceClear);

        return (deletedFiles, filesInUse, msDuration);
    }

    ValueTask<PlayableMedia> InternalGetAsync(string id)
    {
        var pair = cache[id];
        cache[id] = pair with { AccessCount = pair.AccessCount + 1 };
        var info = pair.CachedMediaInfo;
        return ValueTask.FromResult(new PlayableMedia(File.OpenRead(info.MediaFilePath), info, null));
    }

    public async ValueTask<PlayableMedia> GetAsync(string id)
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

    public void TryEditCacheInfo(string id, Action<CachedMediaInfo> editor)
    {
        if (cache.TryGetValue(id, out var info))
        {
           editor(info.CachedMediaInfo);
        }
    }

    public async ValueTask<Stream> InitStreamableCache(MediaInfo info, bool pruneCache = true)
    {
        if (pruneCache)
            await PruneCacheAsync();

        var id = info.Id ?? throw new NullReferenceException("Media ID was null.");

        var fileLegalId = id.ReplaceIllegalCharacters();
        var mediaLocation = Path.Combine(cacheLocation, fileLegalId);
        var file = File.OpenWrite(mediaLocation);

        var cachedMediaInfo = new CachedMediaInfo(mediaLocation, info, true, cacheLocation);
        await cachedMediaInfo.WriteToDisk();

        try { cache.Add(id, new(cachedMediaInfo, 0)); }
        catch (ArgumentException) { }

        return file;
    }
}
