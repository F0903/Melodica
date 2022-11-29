
using Melodica.Services.Downloaders;
using Melodica.Services.Media;

namespace Melodica.Services.Playback.Requests;

public sealed class DownloadRequest : IMediaRequest
{
    public DownloadRequest(ReadOnlyMemory<char> query, IAsyncDownloader dl)
    {
        downloader = dl;
        this.query = query;
    }

    readonly ReadOnlyMemory<char> query;

    readonly IAsyncDownloader downloader;

    MediaInfo? cachedInfo;

    public async Task<MediaInfo> GetInfoAsync()
    {
        return cachedInfo ??= await downloader.GetInfoAsync(query);
    }

    public async Task<MediaCollection> GetMediaAsync()
    {
        var info = await GetInfoAsync();
        return await downloader.DownloadAsync(info);
    }
}
