using Melodica.Services.Downloaders.Exceptions;
using Melodica.Utility;

namespace Melodica.Services.Downloaders;

// One could scrap this and do something clever with generics and static abstract stuff on interfaces, then maybe make Jukebox.PlayAsync take in a generic IAsyncDownloader?
// ¯\_(ツ)_/¯
public static class Downloader
{
    public static IAsyncDownloader YouTube { get; } = new YouTube.AsyncYoutubeDownloader();

    public static IAsyncDownloader Spotify { get; } = new Spotify.AsyncSpotifyDownloader();

    public static IAsyncDownloader SoundCloud { get; } = new SoundCloud.AsyncSoundcloudDownloader();


    public static IAsyncDownloader Default { get; } = YouTube;

    // For convenience
    static readonly IAsyncDownloader[] downloaders =
    {
        YouTube,
        Spotify,
        SoundCloud,
    };

    public static IAsyncDownloader GetFromQuery(string query)
    {
        if (!query.IsUrl())
            return Default;

        foreach (var downloader in downloaders)
        {
            if (downloader.IsUrlSupported(query))
                return downloader;
        }
        throw new UnrecognizedUrlException("URL is not supported!");
    }
}
