using System;

namespace Melodica.Services.Downloaders
{
    [Serializable] // Remember to mark all child classes as Serializable
    public class MediaOrigin
    {
        public MediaOrigin(string serviceName, bool supportsDownloads)
        {
            ServiceName = serviceName;
            SupportsDirectDownloads = supportsDownloads;
        }

        public static MediaOrigin Spotify = new Spotify.SpotifyOrigin();
        public static MediaOrigin YouTube = new YouTube.YouTubeOrigin();
        public static MediaOrigin SoundCloud = new Soundcloud.SoundCloudOrigin();

        public string ServiceName { get; }

        public bool SupportsDirectDownloads { get; }
    }
}
