using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Melodica.Services.Media;
using Melodica.Services.Serialization;

namespace Melodica.Services.Caching;

[Serializable]
public sealed record CachedMediaInfo : MediaInfo
{
    public CachedMediaInfo(string mediaFilePath, MediaInfo info, bool isMediaPartial, string cacheRoot) : base(info)
    {
        this.MediaFilePath = mediaFilePath;
        this.isMediaPartial = isMediaPartial;
        this.cacheRoot = cacheRoot;
    }
    public CachedMediaInfo(string mediaFilePath, string id, bool isMediaPartial, string cacheRoot) : base(id)
    {
        MediaFilePath = mediaFilePath;
        this.isMediaPartial = isMediaPartial;
        this.cacheRoot = cacheRoot;
    }

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
        var metaLocation = Path.Combine(cacheRoot, Path.ChangeExtension(Path.GetFileName(MediaFilePath), CachedMediaInfo.MetaFileExtension));
        await Serializer.SerializeToFileAsync(metaLocation, this);
    }

    async void OnValueChanged()
    {
        await WriteToDisk();
    }

    [NonSerialized]
    readonly string cacheRoot;

    [NonSerialized]
    public const string MetaFileExtension = ".meta";

    [NonSerialized]
    public const uint CurrentMetaVersion = 1;

    public readonly uint MetaVersion = CurrentMetaVersion;

    public readonly string MediaFilePath;

    bool isMediaPartial;
    public bool IsMediaPartial {
        get => isMediaPartial; 
        set
        {
            isMediaPartial = value;
            OnValueChanged();
        }
    }

    bool isMediaInUse;
    public bool IsMediaInUse 
    {
        get => isMediaInUse;
        set
        {
            isMediaInUse = value;
            OnValueChanged();
        } 
    }
}