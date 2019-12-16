using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace PokerBot.Models
{
    public class PlayableMedia
    {
        public PlayableMedia(string name, string path, string format)
        {
            this.Name = name;
            this.Path = path;
            this.Format = format;
        }

        public PlayableMedia(Stream stream, string name, string format)
        {
            Stream = stream;
            Name = name;
            Format = format;
        }

        public Stream? Stream { get; private set; } = null;

        public string? Path { get; private set; } = null;

        public string Name { get; private set; }

        public string Format { get; private set; }
    }
}
