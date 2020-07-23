using System;
using System.Collections.Generic;
using System.Text;

namespace Suits.Jukebox.Models.MediaOrigin
{
    [Serializable] // Remember to mark all child classes as Serializable
    public class MediaOrigin
    {
        public MediaOrigin(string serviceName, bool supportsDirectDownload)
        {
            ServiceName = serviceName;
            SupportsDirectDownload = supportsDirectDownload;
        }

        public static MediaOrigin Spotify = new SpotifyOrigin();
        public static MediaOrigin YouTube = new YouTubeOrigin();

        public string ServiceName { get; }

        public bool SupportsDirectDownload { get; }
    }
}
