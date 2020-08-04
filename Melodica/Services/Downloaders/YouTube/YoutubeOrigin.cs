using System;
using System.Collections.Generic;
using System.Text;

namespace Melodica.Services.Downloaders.YouTube
{
    [Serializable]
    public sealed class YouTubeOrigin : MediaOrigin
    {
        public YouTubeOrigin() : base(serviceName: "YouTube", handlesDownloads: true) { }
    }
}
