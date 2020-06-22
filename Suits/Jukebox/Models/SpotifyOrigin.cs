using System;
using System.Collections.Generic;
using System.Text;

namespace Suits.Jukebox.Models
{
    [Serializable]
    public sealed class SpotifyOrigin : MediaOrigin
    {
        public override string Name { get; protected set; } = "Spotify";
        public override bool SupportsDirectDownload { get; protected set; } = false;
    }   
}
