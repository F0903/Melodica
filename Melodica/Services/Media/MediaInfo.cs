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
    public record MediaInfo
    {
        private static readonly BinarySerializer bin = new();

        public static Task<MediaInfo> LoadFromFile(string fullPath) => bin.DeserializeFileAsync<MediaInfo>(fullPath);

        public const string MetaFileExtension = ".meta";


        public MediaType MediaType { get; init; }

        public TimeSpan Duration { get; set; }

        public string? Id { get; init; }

        public string Title { get; init; } = "External Media";

        public string Artist { get; init; } = "Unknown";

        public string? Url { get; init; }

        public string? ImageUrl { get; init; }

        public DataInfo DataInformation = new();
    }
}