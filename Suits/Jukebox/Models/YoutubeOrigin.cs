using System;
using System.Collections.Generic;
using System.Text;

namespace Suits.Jukebox.Models
{
    [Serializable]
    public sealed class YouTubeOrigin : MediaOrigin
    {
        public override string Name { get; protected set; } = "YouTube";
        public override bool SupportsDirectDownload { get; protected set; } = true;
    }
}
