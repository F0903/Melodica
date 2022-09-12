using System.Collections;

namespace Melodica.Services.Media;

public sealed class MediaCollection : IEnumerable<LazyMedia>
{
    public MediaCollection(IEnumerable<LazyMedia> media, MediaInfo collectionInfo)
    {
        this.media = media;
        CollectionInfo = collectionInfo;
    }

    public MediaCollection(LazyMedia media)
    {
        this.media = new LazyMedia[] { new(media) };
    }

    public MediaInfo? CollectionInfo { get; init; }

    readonly IEnumerable<LazyMedia> media;

    public IEnumerator<LazyMedia> GetEnumerator()
    {
        foreach (LazyMedia? item in media)
        {
            yield return item;
        }
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}
