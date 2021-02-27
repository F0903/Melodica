using System;
using System.Threading.Tasks;

using Melodica.Services.Downloaders;
using Melodica.Services.Serialization;
using Melodica.Utility.Extensions;

namespace Melodica.Services.Media
{
    public enum MediaType
    {
        Video,
        Playlist,
        Livestream
    }

    [Serializable]
    public struct DataInfo
    {
        public string? Format { get; set; }

        public string? FileExtension => Format?.Insert(0, ".");

        public string? MediaPath { get; set; }
    }

    [Serializable]
    public class MediaInfo
    {
        private static readonly BinarySerializer bs = new BinarySerializer();

        public static Task<MediaInfo> LoadFromFile(string fullPath) => bs.DeserializeFileAsync<MediaInfo>(fullPath);

        public const string MetaFileExtension = ".meta";


        public MediaType MediaType { get; set; }

        public TimeSpan Duration { get; set; }

        public string Title { get; set; } = "External Media";

        public string Id { get; init; }

        public string Artist { get; set; } = "";

        public string? Image { get; set; }

        public string? Url { get; set; }

        public DataInfo? DataInformation;
    }
}