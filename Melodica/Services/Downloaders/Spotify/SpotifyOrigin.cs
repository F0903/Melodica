using System;

namespace Melodica.Services.Downloaders.Spotify
{
    [Serializable]
    public sealed class SpotifyOrigin : MediaOrigin
    {
        public SpotifyOrigin() : base(serviceName: "Spotify", supportsDownloads: true)
        {
        } // Set handlesDownloads to true because the downloader class has cross-wiring logic to handle it.
    }
}