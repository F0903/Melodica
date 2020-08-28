using System.ComponentModel;
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

        private IVoiceChannel GetUserVoiceChannel() => ((SocketGuildUser)Context.User).VoiceChannel;

        [Command("Lyrics"), Description("Gets lyrics for a search term.")]
        public async Task GetLyrics([Remainder] string? songName = null)
        {
            Jukebox? juke;
            try { juke = await jukebox.GetJukeboxAsync(Context.Guild, true); }
            catch (System.Exception) { juke = null; }

            if (songName == null && (juke == null || !juke.Playing))
            {
                await ReplyAsync("You either need to specify a search term or have a song playing.");
                return;
            }

            var lyrs = await lyrics.GetLyricsAsync((juke == null || !juke.Playing) ? songName! : juke!.GetSong().info.Title);

            string text = lyrs.Lyrics;
            int count = 0;
            int i = 0;
            while (count != text.Length)
            {
                string currentString = text[count..(text.Length - count < 2048 ? text.Length : 2048)];
                count += currentString.Length;
                EmbedBuilder eb = new EmbedBuilder()
                {
                    Title = i == 0 ? lyrs.Title : "",
                    Description = currentString,
                    ThumbnailUrl = i == 0 ? lyrs.Image : ""
                };
                await ReplyAsync(null, false, eb.Build());
                i++;
            }
        }
    }
}
