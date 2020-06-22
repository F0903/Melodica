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
    public class MediaMetadata
    {
        [Serializable]
        public struct DataInfo
        {
            public string? Format { get; set; }

            public string? FileExtension => Format?.Insert(0, ".");

            public string? MediaPath { get; set; }
        }

        private static readonly BinarySerializer bs = new BinarySerializer();

        public static MediaMetadata LoadFromFile(string fullPath)
        {
            return bs.DeserializeFileAsync<MediaMetadata>(fullPath).GetAwaiter().GetResult();
        }

        public const string MetaFileExtension = ".meta";

        public MediaOrigin? MediaOrigin { get; set; }

        public MediaType MediaType { get; set; }

        public TimeSpan Duration { get; set; }

        public string? Thumbnail { get; set; }

        public string? Title { get; set; }

        public string? URL { get; set; }

        public string? ID { get; set; }

        public DataInfo DataInformation = new DataInfo();
    }
}
