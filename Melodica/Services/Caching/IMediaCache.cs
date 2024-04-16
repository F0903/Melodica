using Melodica.Services.Media;

namespace Melodica.Services.Caching;
public interface IMediaCache
{
    public ValueTask<MediaInfo?> TryGetInfoAsync(string id);
    
    public ValueTask<PlayableMedia?> TryGetAsync(string id);

    public ValueTask TryEditCacheInfo(string id, Func<CachedMediaInfo, CachedMediaInfo> modifier);

    public ValueTask<Stream> InitStreamableCache(MediaInfo info, bool pruneCache = true);
}
