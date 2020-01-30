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

        private static readonly BinaryFormatter bin = new BinaryFormatter(null, new System.Runtime.Serialization.StreamingContext(System.Runtime.Serialization.StreamingContextStates.File));

        public static Task<Metadata> LoadMetadataFromFileAsync(string fullPath)
        {
            return Task.FromResult((Metadata)bin.Deserialize(File.OpenRead(fullPath)));
        }

        public static implicit operator Metadata(string path)
        {
            using var fs = File.OpenRead(path);
            return (Metadata)bin.Deserialize(fs);
        }

        public const string MetaFileExtension = ".meta";

        public string MediaPath { get; set; }

        public string Title { get; }

        public string Extension { get => '.' + Format; }

        public string Format { get; }

        public TimeSpan Duration { get; }
    }
}
