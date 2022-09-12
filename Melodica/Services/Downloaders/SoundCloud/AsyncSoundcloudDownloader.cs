using System.Text.RegularExpressions;

using Melodica.Services.Media;

namespace Melodica.Services.Downloaders.SoundCloud;

internal sealed class AsyncSoundcloudDownloader : IAsyncDownloader
{
    readonly Regex urlRegex = new(@"((https)|(http)):\/\/soundcloud\.com\/.+\/.+\?", RegexOptions.Compiled);

    public Task<MediaCollection> DownloadAsync(MediaInfo info)
    {
        throw new NotImplementedException();
    }

    public Task<MediaInfo> GetInfoAsync(ReadOnlyMemory<char> query)
    {
        throw new NotImplementedException();
    }

    public bool IsUrlSupported(ReadOnlySpan<char> url)
    {
        return urlRegex.IsMatch(url.ToString());
    }
}
