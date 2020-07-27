using System;
using System.Collections.Generic;
using System.Text;

namespace Melodica.Jukebox.Services.Downloaders
{
    public static class DownloaderResolver
    {
        public static AsyncDownloaderBase? GetDownloaderFromURL(string url)
        {
            if (AsyncYoutubeDownloader.IsUrlSupported(url))
                return new AsyncYoutubeDownloader();
            else if (AsyncSpotifyDownloader.IsUrlSupported(url))
                return new AsyncSpotifyDownloader();
            else return null;
        }
    }
}
