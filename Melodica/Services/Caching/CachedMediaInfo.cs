using Melodica.Services.Media;
using Melodica.Services.Serialization;

namespace Melodica.Services.Caching;

[Serializable]
public sealed record CachedMediaInfo : MediaInfo
{
    public CachedMediaInfo(string mediaPath, string cacheRoot, MediaInfo original) : base(original)
    {
        MediaPath = mediaPath;
        this.cacheRoot = cacheRoot;
    }

    // For deserialization purposes.
    public CachedMediaInfo() : base("")
    {
        cacheRoot = "";
        MediaPath = "";
    }

    [NonSerialized]
    private string cacheRoot;

    [NonSerialized]
    public const string MetaFileExtension = ".meta";

    [NonSerialized]
    public const uint CurrentMetaVersion = 1;

    public uint MetaVersion { get; init; } = CurrentMetaVersion;

    public string MediaPath { get; init; }

    public bool IsMediaComplete { get; set; }

    public static async ValueTask<CachedMediaInfo> LoadFromDisk(string path)
    {
        var onDisk = await Serializer.DeserializeFileAsync<CachedMediaInfo>(Path.ChangeExtension(path, MetaFileExtension));
        if (onDisk.MetaVersion < CurrentMetaVersion)
        {
            throw new InvalidOperationException("Meta file version for media is older than current version. Clear cache.");
        }
        return onDisk;
    }

    public async ValueTask WriteToDisk()
    {
        var metaLocation = Path.Combine(cacheRoot, Path.ChangeExtension(Path.GetFileName(MediaPath), MetaFileExtension));
        await Serializer.SerializeToFileAsync(metaLocation, this);
    }
}