using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Runtime.Serialization;
using Suits.Jukebox.Services;
using Suits.Core.Services;

namespace Suits.Jukebox.Models
{
    [Serializable]
    public class Metadata
    {
        public Metadata(string title, string format, TimeSpan duration, string? thumbnailUrl = null)
        {
            Title = title;
            Format = format;
            ThumbnailUrl = thumbnailUrl;
            Duration = duration;
        }

        private static readonly BinarySerializer bs = new BinarySerializer();

        public static Task<Metadata> LoadMetadataFromFileAsync(string fullPath)
        {
            return bs.DeserializeFileAsync<Metadata>(fullPath);
        }     

        public const string MetaFileExtension = ".meta";

        public string? MediaPath { get; set; }

        public string Title { get; }

        public string Extension { get => '.' + Format; }

        public string Format { get; }

        public string? ThumbnailUrl { get; }

        public TimeSpan Duration { get; }
    }
}
