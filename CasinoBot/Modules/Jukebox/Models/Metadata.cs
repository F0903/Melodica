using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace CasinoBot.Modules.Jukebox.Models
{
    [Serializable]
    public class Metadata
    {
        public Metadata(byte[] mediaData, string title, string format, TimeSpan duration)
        {
            this.mediaData = mediaData;
            Title = title;
            Format = format;
            Duration = duration;
        }

        public static implicit operator Metadata(string path)
        {
            var bf = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
            using var fs = File.OpenRead(path);
            return (Metadata)bf.Deserialize(fs);
        }

        private readonly byte[] mediaData;

        public string MediaPath { get; private set; }

        public string Title { get; }

        public string Extension { get => '.' + Format; }

        public string Format { get; }

        public TimeSpan Duration { get; }

        private bool hasReadData = false;

        public byte[] MediaData()
        {
            hasReadData = true;
            return mediaData;
        }

        public void SetPath(string path)
        {
            if (!hasReadData)
                throw new Exception("You cant set the path, because you haven't read any data.");
            MediaPath = path;
        }
    }
}
