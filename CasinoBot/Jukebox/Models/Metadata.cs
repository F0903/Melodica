using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Runtime.Serialization.Formatters.Binary;

namespace CasinoBot.Jukebox.Models
{
    [Serializable]
    public class Metadata
    {
        public Metadata(string title, string format, TimeSpan duration)
        {
            Title = title;
            Format = format;
            Duration = duration;
        }

        public static Task<Metadata> LoadMetadataFromFileAsync(string fullPath)
        {
            var formatter = new BinaryFormatter();
            return Task.FromResult((Metadata)formatter.Deserialize(File.OpenRead(fullPath)));
        }

        public static implicit operator Metadata(string path)
        {
            var bf = new BinaryFormatter();
            using var fs = File.OpenRead(path);
            return (Metadata)bf.Deserialize(fs);
        }

        public const string MetaFileExtension = ".meta";

        public string MediaPath { get; set; }

        public string Title { get; }

        public string Extension { get => '.' + Format; }

        public string Format { get; }

        public TimeSpan Duration { get; }
    }
}
