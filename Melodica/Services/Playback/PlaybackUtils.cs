using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Discord;

using Melodica.Services.Media;

namespace Melodica.Services.Playback
{
    public static class PlaybackUtils
    {
        public static Color MediaStateToColor(MediaState state) => state switch
        {
            MediaState.Error => Color.Red,
            MediaState.Queued => Color.DarkGrey,
            MediaState.Downloading => Color.Blue,
            MediaState.Playing => Color.Green,
            MediaState.Finished => Color.LighterGrey,
            _ => Color.Default,
        };

        public static Embed CreateMediaEmbed(MediaInfo info, MediaInfo? playlistInfo, MediaState state)
        {
            const char InfChar = '\u221E';

            var color = MediaStateToColor(state);

            var description = playlistInfo != null ? $"__{info.Title}__\n{playlistInfo.Title}" : info.Title;

            bool durationUnknown = info.MediaType == MediaType.Livestream || info.Duration == TimeSpan.Zero;
            var footer = durationUnknown ? InfChar.ToString() : $"{info.Duration}{(playlistInfo is not null ? $" | {playlistInfo.Duration}" : "")}";

            var embed = new EmbedBuilder()
                        .WithColor(color)
                        .WithTitle(info.Artist)
                        .WithDescription(description)
                        .WithFooter(footer)
                        .WithThumbnailUrl(info.ImageUrl)
                        .Build();
            return embed;
        }
    }
}
