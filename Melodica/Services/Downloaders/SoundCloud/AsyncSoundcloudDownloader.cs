using System.Text.RegularExpressions;

using Melodica.Services.Media;

namespace Melodica.Services.Downloaders.SoundCloud;

internal sealed partial class AsyncSoundcloudDownloader : IAsyncDownloader
{
    [GeneratedRegex("((https)|(http)):\\/\\/soundcloud\\.com\\/.+\\/.+\\?", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex SoundcloudUrlRegex(); 

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
        return SoundcloudUrlRegex().IsMatch(url.ToString());
    } 
}
