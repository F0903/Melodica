using System.Collections;

namespace Melodica.Services.Media;

public sealed class MediaCollection(IEnumerable<LazyMedia> media, MediaInfo? collectionInfo) : IEnumerable<LazyMedia>
{
    public MediaInfo? CollectionInfo { get; init; } = collectionInfo;

    readonly IEnumerable<LazyMedia> media = media;

    public static MediaCollection WithOne(LazyMedia media) => new([media], null);

    public IEnumerator<LazyMedia> GetEnumerator()
    {
        foreach (var item in media)
        {
            yield return item;
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
