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

        public readonly static MediaOrigin Spotify = new Spotify.SpotifyOrigin();
        public readonly static MediaOrigin YouTube = new YouTube.YouTubeOrigin();
        public readonly static MediaOrigin SoundCloud = new Soundcloud.SoundCloudOrigin();

        public string ServiceName { get; }

        public bool SupportsDirectDownloads { get; }
    }
}