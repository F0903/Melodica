
using Discord;

using Melodica.Services.Media;

namespace Melodica.Services.Playback;

public static class EmbedUtils
{
    public static Embed CreateMediaEmbed(MediaInfo info, MediaInfo? collectionInfo)
    {
        const char InfChar = '\u221E';

        string? description = (info.MediaType == MediaType.Video && collectionInfo != null) ? $"__[{info.Title}]({info.Url})__\n{collectionInfo.Title}" : info.Title;

        bool durationUnknown = info.MediaType == MediaType.Livestream || info.Duration == TimeSpan.Zero;
        string? durationStr = $"{info.Duration}{(info.MediaType != MediaType.Playlist && collectionInfo is not null ? $" | {collectionInfo.Duration}" : "")}";
        string? footer = durationUnknown ? InfChar.ToString() : durationStr;

        Embed? embed = new EmbedBuilder()
                    .WithTitle(info.Artist)
                    .WithDescription(description)
                    .WithFooter(footer)
                    .WithThumbnailUrl(info.ImageUrl)
                    .Build();
        return embed;
    }
}
