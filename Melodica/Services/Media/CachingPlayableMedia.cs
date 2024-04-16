using Melodica.Services.Caching;
using Melodica.Utility;

namespace Melodica.Services.Media;
public class CachingPlayableMedia(AsyncParameterizedLazyGetter<Stream, MediaInfo> dataSource, AsyncLazyGetter<MediaInfo> infoSource, PlayableMedia? next, IMediaCache? cachingProvider) : PlayableMedia(dataSource, infoSource, next)
{
    private Stream? cachingStream;
    private bool cacheFinished = false;

    private async ValueTask SwitchToCacheStream()
    {
        (await GetDataAsync()).Close();

        if (cachingStream is null) throw new NullReferenceException("Caching stream was null when trying to switch.");

        DataSource = cachingStream;

        cachingProvider = null;
        cachingStream = null;
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        var info = await GetInfoAsync(cancellationToken);
        var read = await base.ReadAsync(buffer, cancellationToken);

        if (!cacheFinished)
        {
            //TODO: Fix this gets run for some reason when skipping a media that has been played before, even though the cancellation token in here is not marked as cancelled.
            if (read == 0) // No more input; we are done
            {
                if (cachingProvider is not null)
                {
                    await cachingProvider.TryEditCacheInfo(info.Id, x =>
                    {
                        x.IsComplete = true;
                        return x;
                    });
                    await SwitchToCacheStream();
                    cacheFinished = true;
                }
                return 0;
            }

            if (cachingProvider is not null)
            {
                cachingStream ??= await cachingProvider.InitStreamableCache(await GetInfoAsync(cancellationToken));
                if (cachingStream is not null)
                    await cachingStream.WriteAsync(buffer[..read], cancellationToken);
            }
        }

        return read;
    }

    public override Task CloseAsync() 
    {
        cachingStream?.Close();
        return base.CloseAsync();
    }
}
