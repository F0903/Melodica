using System;
using System.Collections.Generic;
using System.Text;

namespace PokerBot.Models
{
    public class DownloadResult
    {
        public DownloadResult(string name, string path, string format)
        {
            this.name = name;
            this.path = path;
            this.format = format;
        }

        public DownloadResult((string name, string path, string format)[] videos)
        {
            this.playlist = videos;
        }

        readonly (string name, string path, string format)[] playlist;

        readonly string name, path, format;

        public readonly bool isPlaylist;

        public (string name, string path, string format)[] GetResult() =>
            isPlaylist ? playlist : new[] { (this.name, this.path, this.format) };
    }
}
