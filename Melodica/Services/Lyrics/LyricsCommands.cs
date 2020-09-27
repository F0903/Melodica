using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

using Discord;
using Discord.Commands;
using Discord.WebSocket;

using Melodica.Services.Playback;

namespace Melodica.Services.Lyrics
{
    public class LyricsCommands : ModuleBase<SocketCommandContext>
    {
        public LyricsCommands(LyricsProvider lyrics, JukeboxProvider jukebox)
        {
            this.lyrics = lyrics;
            this.jukebox = jukebox;
        }

        readonly LyricsProvider lyrics;
        readonly JukeboxProvider jukebox;

        [Command("Lyrics"), Description("Gets lyrics for a search term.")]
        public async Task GetLyrics([Remainder] string? songName = null)
        {
            Jukebox? juke;
            try { juke = await jukebox.GetJukeboxAsync(Context.Guild, true); }
            catch (System.Exception) { juke = null; }

            if (songName == null && !(juke != null && juke.Playing))
            {
                await ReplyAsync("You either need to specify a search term or have a song playing.");
                return;
            }

            if (songName == null)
            {
                var songInfo = juke!.GetSong().info;
                songName = $"{songInfo.Artist} {songInfo.Title}";
            }

            Stopwatch timer = new Stopwatch();
            var lyrs = await lyrics.GetLyricsAsync(songName);
            var text = lyrs.Lyrics;
            int count = 0;
            int i = 0;
            while (count < text.Length)
            {
                timer.Start();
                var outText = text.Substring(i * 2048, Math.Min(2048, text.Length - count));
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
