using System;
using System.Collections.Generic;
using System.Text;

namespace Suits.Jukebox.Models
{
    [Serializable] // Remember to mark all child classes as Serializable
    public abstract class MediaOrigin
    {
        public static MediaOrigin Spotify = new SpotifyOrigin();
        public static MediaOrigin YouTube = new YouTubeOrigin();

        public abstract string Name { get; protected set; }

        public abstract bool SupportsDirectDownload { get; protected set; }
    }
}
