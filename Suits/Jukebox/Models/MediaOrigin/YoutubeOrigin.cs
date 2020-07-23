using System;
using System.Collections.Generic;
using System.Text;

namespace Suits.Jukebox.Models.MediaOrigin
{
    [Serializable]
    public sealed class YouTubeOrigin : MediaOrigin
    {
        public YouTubeOrigin() : base(serviceName: "YouTube", supportsDirectDownload: true) { }
    }
}
