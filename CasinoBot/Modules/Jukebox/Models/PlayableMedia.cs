using System.IO;

namespace CasinoBot.Modules.Jukebox.Models
{
#nullable enable

    public class PlayableMedia
    {
        public PlayableMedia(string name, string path, string format, int lengthInSec)
        {
            this.Title = name;
            this.Path = path;
            this.Format = format;
            SecondDuration = lengthInSec;
        }

        public PlayableMedia(Stream stream, string name, string format, int lengthInSec)
        {
            Stream = stream;
            Title = name;
            Format = format;
            SecondDuration = lengthInSec;
        }

        public Stream? Stream { get; } = null;

        public string? Path { get; } = null;

        public string Title { get; }

        public string Format { get; }

        public int SecondDuration { get; }
    }
}