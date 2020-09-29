using System.Threading.Tasks;

using Discord;
using Discord.Commands;

using Melodica.Services.Playback;

namespace Melodica.Services.Wiki
{
    public class WikiCommands : ModuleBase<SocketCommandContext>
    {
        public WikiCommands(WikiProvider wiki, JukeboxProvider jukebox)
        {
            this.wiki = wiki;
            jukeboxProvider = jukebox;
        }

        private readonly WikiProvider wiki;
        private readonly JukeboxProvider jukeboxProvider;

        private Jukebox Jukebox => jukeboxProvider.GetJukeboxAsync(Context.Guild).Result;

        [Command("Info"), Summary("Gets info from a wiki for the specified page.")]
        public async Task InfoAsync([Remainder] string? pageTitle = null)
        {
            if (pageTitle == null && !Jukebox.Playing)
            {
                await ReplyAsync("You must have a song playing when using this command with no parameter.");
                return;
            }

            WikiElement info;
            if (pageTitle == null && Jukebox.Playing)
            {
                string? artist = Jukebox.GetSong().info.Artist;

                info = await wiki.GetInfoAsync(artist);
            }
            else
            {
                info = await wiki.GetInfoAsync(pageTitle!);
            }

            await ReplyAsync(null, false, new EmbedBuilder()
            {
                ThumbnailUrl = info.ImageUrl,
                Title = info.Title,
                Description = info.Info
            }.Build());
        }
    }
}
