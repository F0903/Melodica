using System;
using System.Collections.Generic;
using System.Text;

namespace Suits.Jukebox.Services.Downloaders
{
    public static class DownloaderResolver
    {
        public static AsyncDownloaderBase? GetDownloader(string url)
        {
            if (url.Contains("https://www.youtube.com/"))
                return new AsyncYoutubeDownloader();
            else if (url.Contains("https://open.spotify.com/"))
                return new AsyncSpotifyDownloader();
            else return null;
        }
    }
}
