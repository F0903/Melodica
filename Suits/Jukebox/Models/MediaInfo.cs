using System;
using System.Collections.Generic;
using System.Text;

namespace Suits.Jukebox.Models
{
    [Serializable]
    public struct MediaInfo : IMediaInfo
    {
        public TimeSpan Duration { get; set; }

        public string? Thumbnail { get; set; }

        public string Title { get; set; }

        public string? URL { get; set; }

        public string? ID { get; set; }

        public TimeSpan GetDuration() => Duration;

        public string? GetThumbnail() => Thumbnail;

        public string GetTitle() => Title;

        public string? GetURL() => URL;

        public string? GetID() => ID;
    }
}
