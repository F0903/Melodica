using System;
using System.Collections.Generic;
using System.Text;

namespace Suits.Jukebox.Models.Origins
{
    [Serializable] // Remember to mark all child classes as Serializable
    public class MediaOrigin
    {
        public MediaOrigin(string serviceName, bool handlesDownloads)
        {
            ServiceName = serviceName;
            HandlesDownloads = handlesDownloads;
        }

        public static MediaOrigin Spotify = new SpotifyOrigin();
        public static MediaOrigin YouTube = new YouTubeOrigin();

        public string ServiceName { get; }

        public bool HandlesDownloads { get; }
    }
}
