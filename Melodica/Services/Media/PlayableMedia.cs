using Melodica.Services.Caching;

namespace Melodica.Services.Media;

// Format string just gets passed along raw to FFmpeg.
public record DataPair(Stream? Data, string Format);

public delegate Task<DataPair> DataGetter(PlayableMedia media);

// Perhaps seperate into different classes.
public class PlayableMedia(MediaInfo info, DataGetter? dataGetter)
{
    public PlayableMedia(MediaInfo info, MediaInfo? collectionInfo, DataGetter dataGetter, IMediaCache? cache) : this(info, dataGetter)
    {
        CollectionInfo = collectionInfo;
        this.cache = cache;
    }

    protected DataGetter? DataGetter { get; init; } = dataGetter;

    [NonSerialized]
    private readonly IMediaCache? cache;

    [NonSerialized]
    MediaInfo? collectionInfo;
    public MediaInfo? CollectionInfo { get => collectionInfo; set => collectionInfo = value; }

    public MediaInfo Info { get; set; } = info;

    public static ValueTask<PlayableMedia> FromExisting(MediaInfo info)
    {
        PlayableMedia media = new(info, null);
        return ValueTask.FromResult(media);
    }

    public virtual async Task<DataInfo> GetDataAsync()
    {
        if (cache is null)
            throw new NullReferenceException("Cache was null.");

        if (cache.TryGet(Info.Id, out var cachedMedia))
        {
            var info = cachedMedia!.Info.DataInfo;
            if (info is not null)
                return info;
        }

        if (DataGetter is null)
            throw new NullReferenceException("DataGetter was null.");

        // Write the media data to file.
        if (Info.DataInfo is null)
        {
            var dataPair = await DataGetter(this);
            return Info.DataInfo = await cache.CacheAsync(this, dataPair);
        }
        return Info.DataInfo;
    }
}
