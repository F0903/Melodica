using System;
using System.Collections.Generic;
using System.Text;

namespace Melodica.Services.Downloaders
{
    [Serializable] // Remember to mark all child classes as Serializable
    public class MediaOrigin
    {
        public MediaOrigin(string serviceName, bool handlesDownloads)
        {
            ServiceName = serviceName;
            HandlesDownloads = handlesDownloads;
        }

        public static MediaOrigin Spotify = new Spotify.SpotifyOrigin();
        public static MediaOrigin YouTube = new YouTube.YouTubeOrigin();

        public string ServiceName { get; }

        public bool HandlesDownloads { get; }
    }
}
