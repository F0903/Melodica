using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Melodica.Services.Media;

namespace Melodica.Services.Caching;
public interface IMediaCache
{
    public ValueTask<MediaInfo?> TryGetInfoAsync(string id);
    
    public ValueTask<PlayableMediaStream?> TryGetAsync(string id);

    public ValueTask TryEditCacheInfo(string id, Func<CachedMediaInfo, CachedMediaInfo> modifier);

    public ValueTask<Stream> InitStreamableCache(MediaInfo info, bool pruneCache = true);
}
