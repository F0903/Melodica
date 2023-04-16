using Melodica.Services.Serialization;

namespace Melodica.Services.Media;

public enum MediaType
{
    Video,
    Playlist,
    Livestream
}

[Serializable]
public record DataInfo(string? Format, string? MediaPath)
{
    public string FileExtension => Format is not null ? Format.Insert(0, ".") : "";
}

[Serializable]
public record MediaInfo(string Id)
{
    private static readonly BinarySerializer bin = new();

    public static Task<MediaInfo> LoadFromFile(string fullPath)
    {
        return bin.DeserializeFileAsync<MediaInfo>(fullPath);
    }

    public const string MetaFileExtension = ".meta";

    public MediaType MediaType { get; init; }

    public TimeSpan Duration { get; set; }

    public string Title { get; init; } = "External Media";

    public string Artist { get; init; } = "Unknown";

    public string? Url { get; init; }

    public string? ImageUrl { get; init; }

    public DataInfo? DataInfo { get; set; }
}
