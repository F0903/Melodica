
using Discord;

using Melodica.Services.Media;

using System.Text;

namespace Melodica.Services.Playback;

public static class EmbedUtils
{
    public static Embed CreateMediaEmbed(MediaInfo info, MediaInfo? collectionInfo)
    {
        const char InfChar = '\u221E';

        StringBuilder description = new($"[{info.Title}]({info.Url})");
        if (collectionInfo != null)
        {
            description.Insert(0, "__");
            description.Append($"__\n{collectionInfo.Title}");
        }

        var durationUnknown = info.MediaType == MediaType.Livestream || info.Duration == TimeSpan.Zero;
        var durationStr = $"{info.Duration}{(info.MediaType != MediaType.Playlist && collectionInfo is not null ? $" | {collectionInfo.Duration}" : "")}";
        var footer = durationUnknown ? InfChar.ToString() : durationStr;

        var embed = new EmbedBuilder()
                    .WithTitle(info.Artist)
                    .WithDescription(description.ToString())
                    .WithFooter(footer)
                    .WithThumbnailUrl(info.ImageUrl)
                    .Build();
        return embed;
    }
}
