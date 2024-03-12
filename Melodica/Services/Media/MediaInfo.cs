namespace Melodica.Services.Media;

public enum MediaType
{
    Video,
    Playlist,
    Livestream
}

[Serializable]
public record MediaInfo(string Id)
{
    public MediaType MediaType { get; init; }

    public TimeSpan Duration { get; set; }

    public string Title { get; init; } = "External Media";

    public string Artist { get; init; } = "Unknown";

    public string? Url { get; init; }

    public string? ImageUrl { get; init; }

    public string? ExplicitDataFormat { get; set; }
}
