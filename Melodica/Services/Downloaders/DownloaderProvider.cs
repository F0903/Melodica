using System;
using System.Collections.Generic;
using System.Text;
using Melodica.Services.Downloaders;
using Melodica.Services.Downloaders.Spotify;
using Melodica.Services.Downloaders.YouTube;

namespace Melodica.Services.Services.Downloaders
{
    public class DownloaderProvider
    {
        public AsyncDownloaderBase? GetDownloaderFromURL(string url)
        {
            if (AsyncYoutubeDownloader.IsUrlSupported(url))
                return new AsyncYoutubeDownloader();
            else if (AsyncSpotifyDownloader.IsUrlSupported(url))
                return new AsyncSpotifyDownloader();
            else return null;
        }
    }
}
