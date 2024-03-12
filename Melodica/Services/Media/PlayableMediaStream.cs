using Discord.Interactions;
using Melodica.Services.Caching;
using Melodica.Utility;

namespace Melodica.Services.Media;

public class PlayableMediaStream(AsyncParameterizedLazyGetter<Stream, MediaInfo> dataSource, AsyncLazyGetter<MediaInfo> infoSource, PlayableMediaStream? next, IMediaCache? cachingProvider) : Stream
{
    private readonly AsyncParameterizedLazyGetter<Stream, MediaInfo> dataSource = dataSource;

    private Task<Stream>? cachedData;
    protected Task<Stream> GetDataAsync() => cachedData ??= GetInfoAsync().Chain(dataSource.GetAsync);

    // When this is called, the async version has most likely already run, meaning it wont block for long.
    protected Stream GetData() => cachedData is not null ? cachedData.Result : GetDataAsync().GetAwaiter().GetResult();

    private Task<MediaInfo>? cachedInfo;
    public Task<MediaInfo> GetInfoAsync() => cachedInfo ??= infoSource.GetAsync();
    public void SetInfo(MediaInfo info) => cachedInfo = Task.FromResult(info);

    public PlayableMediaStream? Next { get; set; } = next;

    private Stream? cachingStream;

    public override bool CanRead { get; } = true;
    public override bool CanSeek { get; } = false;
    public override bool CanWrite { get; } = false;
    public override long Length => GetData().Length;
    public override long Position { get => GetData().Position; set => GetData().Position = value; }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        var data = await GetDataAsync();
        var info = await GetInfoAsync();
        var read = await data.ReadAsync(buffer, cancellationToken);

        if (cachingProvider is not null)
        {
            if (cachingStream is not null)
            {
                await cachingStream.WriteAsync(buffer[..read], cancellationToken);

                if (read == 0)
                {
                    await cachingProvider.TryEditCacheInfo(info.Id, x =>
                    {
                        x.IsMediaComplete = true;
                        return x;
                    });
                }
            }
            else
            {
                cachingStream = await cachingProvider.InitStreamableCache(await GetInfoAsync());
                await cachingProvider.TryEditCacheInfo(info.Id, x =>
                {
                    x.IsMediaComplete = false;
                    return x;
                });
            }
        }

        return read;
    }

    public override void Flush() => GetData().Flush();
    public override async Task FlushAsync(CancellationToken cancellationToken) => await (await GetDataAsync()).FlushAsync(cancellationToken);
    public override int Read(byte[] buffer, int offset, int count) => throw new NotImplementedException("Use async read.");
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    public override void Close()
    {
        GetData().Close();
        cachingStream?.Close();
    }

    public void CloseAll()
    {
        Close();
        Next?.CloseAll();
    }
}
