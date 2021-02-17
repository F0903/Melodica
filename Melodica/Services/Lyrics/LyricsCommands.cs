using System;
using System.ComponentModel;
using System.Threading.Tasks;

using Discord;
using Discord.Commands;

using Melodica.Services.Playback;

namespace Melodica.Services.Lyrics
{
    public class LyricsCommands : ModuleBase<SocketCommandContext>
    {
        public LyricsCommands(ILyricsProvider lyrics)
        {
            this.lyrics = lyrics;
        }

        private readonly ILyricsProvider lyrics;

        [Command("Lyrics"), Description("Gets lyrics for a search term.")]
        public async Task GetLyrics([Remainder] string? songName = null)
        {
            Jukebox? juke;
            try { juke = await JukeboxFactory.GetJukeboxAsync(Context.Guild); }
            catch (Exception) { juke = null; }

            if (songName == null && !(juke != null && juke.Playing))
            {
                await ReplyAsync("You either need to specify a search term or have a song playing.");
                return;
            }

            if (songName == null)
            {
                var songInfo = juke!.Song!.Value.info;
                songName = $"{songInfo.Artist} {songInfo.Title}";
            }

            var lyrs = await lyrics.GetLyricsAsync(songName);
            ReadOnlyMemory<char> text = lyrs.Lyrics.AsMemory();
            const int pageSize = 2048;
            for (int i = 0; i < text.Length; i += pageSize)
            {
                string pageText = text.Span.Slice(i, Math.Min(pageSize, text.Length - i)).ToString();
                await ReplyAsync(null, false, new EmbedBuilder()
                {
                    Title = i == 0 ? lyrs.Title : "",
                    ThumbnailUrl = i == 0 ? lyrs.Image : "",
                    Description = pageText
                }.Build());
            }
        }
    }
}