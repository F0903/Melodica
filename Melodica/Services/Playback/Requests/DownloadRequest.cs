using Melodica.Services.Downloaders;
using Melodica.Services.Media;

namespace Melodica.Services.Playback.Requests;

public sealed class DownloadRequest(ReadOnlyMemory<char> query, IAsyncDownloader dl) : IMediaRequest
{
    MediaInfo? cachedInfo;

    public async Task<MediaInfo> GetInfoAsync() => cachedInfo ??= await dl.GetInfoAsync(query);

    public async Task<MediaCollection> GetMediaAsync()
    {
        var info = await GetInfoAsync();
        return await dl.DownloadAsync(info);
    }
}
