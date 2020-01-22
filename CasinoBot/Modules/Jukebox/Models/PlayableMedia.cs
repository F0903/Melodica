using System;
using System.IO;

namespace CasinoBot.Modules.Jukebox.Models
{
    public class PlayableMedia : IMediaInfo
    {
        public PlayableMedia(Metadata meta)
        {
            this.Meta = meta;
        }

        public PlayableMedia(PlayableMedia toCopy)
        {
            Meta = toCopy.Meta;
        }

        public Metadata Meta { get; protected set; }

        public string MediaPath { get => Meta.MediaPath; }

        public TimeSpan GetDuration() => Meta.Duration;
        public string GetTitle() => Meta.Title;
    }
}