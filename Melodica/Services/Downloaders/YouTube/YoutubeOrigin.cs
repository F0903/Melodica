using System;

namespace Melodica.Services.Downloaders.YouTube
{
    [Serializable]
    public sealed class YouTubeOrigin : MediaOrigin
    {
        public YouTubeOrigin() : base(serviceName: "YouTube", supportsDownloads: true) { }
    }
}
