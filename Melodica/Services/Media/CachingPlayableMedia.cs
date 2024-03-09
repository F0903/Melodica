using Melodica.Services.Caching;

namespace Melodica.Services.Media;

public class CachingPlayableMedia : PlayableMedia
{
    public CachingPlayableMedia(Stream data, MediaInfo info, MediaFileCache cachingProvider, PlayableMedia? next) : base(data, info, next)
    {
        this.cachingProvider = cachingProvider;
        cachingProvider.TryEditCacheInfo(info.Id, x => x.IsMediaInUse = true);
    }

    readonly MediaFileCache cachingProvider;

    Stream? cacheStream;
    bool isCacheComplete = false;

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        cacheStream ??= await cachingProvider.InitStreamableCache(Info);

        var read = await base.ReadAsync(buffer, cancellationToken);

        if (read == 0 && !isCacheComplete)
        {
            isCacheComplete = true;
            cachingProvider.TryEditCacheInfo(Info.Id, x => x.IsMediaPartial = false);
        }

        await cacheStream!.WriteAsync(buffer[..read], cancellationToken);
        return read;
    }

    public override void Close()
    {
        cachingProvider.TryEditCacheInfo(Info.Id, x => x.IsMediaInUse = false);
        cacheStream?.Close();
        base.Close();
    }
}
