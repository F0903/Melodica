using Melodica.Utility;
using Melodica.Utility.Extensions;

namespace Melodica.Services.Media;

public class PlayableMedia(AsyncParameterizedLazyGetter<Stream, MediaInfo> dataSource, AsyncLazyGetter<MediaInfo> infoSource, PlayableMedia? next)
{
    public PlayableMedia? Next { get; set; } = next;

    protected AsyncParameterizedLazyGetter<Stream, MediaInfo> DataSource { get; set; } = dataSource;
    protected AsyncLazyGetter<MediaInfo> InfoSource { get; set; } = infoSource;

    protected async Task<Stream> GetDataAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var data = await InfoSource.GetAsync().Chain(DataSource.GetAsync);
        return data;
    }

    public async Task<MediaInfo> GetInfoAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var info = await InfoSource.GetAsync();
        return info;
    }

    public virtual async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        var data = await GetDataAsync(cancellationToken);
        var read = await data.ReadAsync(buffer, cancellationToken);
        return read;
    }

    public virtual async Task CloseAsync()
    {
        var data = await GetDataAsync();
        data.Close();
    }

    public async Task CloseAllAsync()
    {
        await CloseAsync();
        if (Next is not null) await Next.CloseAllAsync();
    }
}