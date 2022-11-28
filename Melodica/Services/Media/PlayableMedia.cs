using Melodica.Services.Caching;

namespace Melodica.Services.Media;

// Format string just gets passed along raw to FFmpeg.
public record DataPair(Stream? Data, string Format);

public delegate Task<DataPair> DataGetter(PlayableMedia media);

public class PlayableMedia
{
    // Should be a seperate class.
    private PlayableMedia(MediaInfo meta)
    {
        Info = meta;
    }

    public PlayableMedia(MediaInfo info, MediaInfo? collectionInfo, DataGetter dataGetter, IMediaCache? cache)
    {
        Info = info;
        CollectionInfo = collectionInfo;
        this.dataGetter = dataGetter;
        this.cache = cache;
    }

    [NonSerialized]
    private readonly DataGetter? dataGetter;

    [NonSerialized]
    private readonly IMediaCache? cache;

    [NonSerialized]
    MediaInfo? collectionInfo;
    public MediaInfo? CollectionInfo { get => collectionInfo; set => collectionInfo = value; }

    public MediaInfo Info { get; set; }

    // Should be a seperate class.
    public static ValueTask<PlayableMedia> FromExisting(MediaInfo info)
    {
        var media = new PlayableMedia(info);
        return ValueTask.FromResult(media);
    }

    public virtual async Task<DataInfo> GetDataAsync()
    {
        if (cache is null)
            throw new NullReferenceException("Cache was null.");

        if (cache.TryGet(Info.Id, out PlayableMedia? cachedMedia))
        {
            DataInfo? info = cachedMedia!.Info.DataInfo;
            if (info is not null)
                return info;
        }

        if (dataGetter is null)
            throw new NullReferenceException("DataGetter was null. (1)");

        // Write the media data to file.
        if (Info.DataInfo is null)
        {
            var dataPair = await dataGetter(this);
            return Info.DataInfo = await cache.CacheAsync(this, dataPair);
        }
        return Info.DataInfo;
    }
}
