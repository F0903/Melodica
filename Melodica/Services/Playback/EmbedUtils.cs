﻿
using Discord;

using Melodica.Services.Media;

namespace Melodica.Services.Playback;

public static class EmbedUtils
{
    public static Embed SetEmbedToState(this IEmbed e, MediaState state)
    {
        EmbedBuilder? builder = new EmbedBuilder
        {
            Color = MediaStateToColor(state),
            Title = e.Title,
            Description = e.Description,
            Footer = new EmbedFooterBuilder().WithText(e.Footer?.ToString()),
            ThumbnailUrl = e.Thumbnail?.ToString()
        };
        return builder.Build();
    }

    public static Color MediaStateToColor(MediaState state)
    {
        return state switch
        {
            MediaState.Error => Color.Red,
            MediaState.Queued => Color.DarkGrey,
            MediaState.Downloading => Color.Blue,
            MediaState.Playing => Color.Green,
            MediaState.Finished => Color.LighterGrey,
            _ => Color.Default,
        };
    }

    //TODO: Figure out if we should remove MediaState?
    public static Embed CreateMediaEmbed(MediaInfo info, MediaInfo? collectionInfo, MediaState state)
    {
        const char InfChar = '\u221E';

        Color color = MediaStateToColor(state);

        string? description = (info.MediaType == MediaType.Video && collectionInfo != null) ? $"__[{info.Title}]({info.Url})__\n{collectionInfo.Title}" : info.Title;

        bool durationUnknown = info.MediaType == MediaType.Livestream || info.Duration == TimeSpan.Zero;
        string? durationStr = $"{info.Duration}{(info.MediaType != MediaType.Playlist && collectionInfo is not null ? $" | {collectionInfo.Duration}" : "")}";
        string? footer = durationUnknown ? InfChar.ToString() : durationStr;

        Embed? embed = new EmbedBuilder()
                    .WithColor(color)
                    .WithTitle(info.Artist)
                    .WithDescription(description)
                    .WithFooter(footer)
                    .WithThumbnailUrl(info.ImageUrl)
                    .Build();
        return embed;
    }
}
