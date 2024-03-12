using Melodica.Services.Caching;
using Melodica.Services.Downloaders;
using Melodica.Services.Media;

namespace Melodica.Services.Playback.Requests;

public sealed class DownloaderRequest(ReadOnlyMemory<char> query, IAsyncDownloader dl) : IMediaRequest
{
    MediaInfo? cachedInfo;

    PlayableMediaStream? cachedMedia;

    public async Task<MediaInfo> GetInfoAsync() => cachedInfo ??= await dl.GetInfoAsync(query);

    public async Task<PlayableMediaStream> GetMediaAsync()
    {
        var info = await GetInfoAsync();
        return cachedMedia ??= await dl.DownloadAsync(info);
    }
}
