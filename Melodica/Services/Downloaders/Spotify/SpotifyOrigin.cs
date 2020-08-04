using System;
using System.Collections.Generic;
using System.Text;
using Melodica.Services.Downloaders;

namespace Melodica.Services.Downloaders.Spotify
{
    [Serializable]
    public sealed class SpotifyOrigin : MediaOrigin
    {
        public SpotifyOrigin() : base(serviceName: "Spotify", handlesDownloads: true) { } // Set handlesDownloads to true because the downloader class has cross-wiring logic to handle it.
    }   
}
