
using Melodica.Services.Downloaders.YouTube;
using Melodica.Services.Media;

namespace Melodica.Services.Downloaders;

public interface IAsyncDownloader
{  
    public bool IsUrlSupported(ReadOnlySpan<char> url);

    public Task<MediaInfo> GetInfoAsync(ReadOnlyMemory<char> query);

    public Task<MediaCollection> DownloadAsync(MediaInfo info);
}
