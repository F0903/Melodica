using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading.Tasks;

using Discord;
using Discord.Commands;

using Melodica.Services.Playback;

namespace Melodica.Services.Lyrics
{
    public class LyricsCommands : ModuleBase<SocketCommandContext>
    {
        public LyricsCommands(LyricsProvider lyrics, JukeboxProvider jukebox)
        {
            this.lyrics = lyrics;
            this.jukeboxProvider = jukebox;
        }

        private readonly LyricsProvider lyrics;
        private readonly JukeboxProvider jukeboxProvider;

        [Command("Lyrics"), Description("Gets lyrics for a search term.")]
        public async Task GetLyrics([Remainder] string? songName = null)
        {
            Jukebox? juke;
            try { juke = await jukeboxProvider.GetJukeboxAsync(Context.Guild); }
            catch (System.Exception) { juke = null; }

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

            var timer = new Stopwatch();
            var lyrs = await lyrics.GetLyricsAsync(songName);
            string? text = lyrs.Lyrics;
            int count = 0;
            int i = 0;
            while (count < text.Length)
            {
                timer.Start();
                string? outText = text.Substring(i * 2048, Math.Min(2048, text.Length - count));
                await ReplyAsync(null, false, new EmbedBuilder()
                {
                    Title = i == 0 ? lyrs.Title : "",
                    ThumbnailUrl = i == 0 ? lyrs.Image : "",
                    Description = outText
                }.Build());
                count += outText.Length;
                ++i;
            }
            timer.Stop();
        }
    }
}
