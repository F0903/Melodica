using System;

using Melodica.Services.Downloaders;
using Melodica.Services.Serialization;

namespace Melodica.Services.Models
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

        public static MediaMetadata LoadFromFile(string fullPath) => bs.DeserializeFileAsync<MediaMetadata>(fullPath).GetAwaiter().GetResult();

        public const string MetaFileExtension = ".meta";

        public MediaOrigin Origin { get; set; } = new MediaOrigin("External", false);

        public MediaType MediaType { get; set; }

        public TimeSpan Duration { get; set; }

        public string Title { get; set; } = "External Media";

        public string Artist { get; set; } = "";

        public string? Thumbnail { get; set; }

        public string? Url { get; set; }

        public string? Id { get; set; }

        public DataInfo DataInformation = new DataInfo();
    }
}
