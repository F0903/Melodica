using System;
using System.Collections.Generic;
using System.Text;

namespace Suits.Jukebox.Models.MediaOrigin
{
    [Serializable]
    public sealed class SpotifyOrigin : MediaOrigin
    {
        public SpotifyOrigin() : base(serviceName: "Spotify", supportsDirectDownload: false) { }
    }   
}
