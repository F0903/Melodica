using System.Threading.Tasks;

using Discord;
using Discord.Commands;

using Melodica.Services.Playback;

namespace Melodica.Services.Wiki
{
    public class WikiCommands : ModuleBase<SocketCommandContext>
    {
        public WikiCommands(IWikiProvider wiki)
        {
            this.wiki = wiki;
        }

        private readonly IWikiProvider wiki;

        private Jukebox GetJukebox => JukeboxFactory.GetJukeboxAsync(Context.Guild).Result;

        [Command("Info"), Alias("Wiki"), Summary("Gets info from a wiki for the specified page.")]
        public async Task InfoAsync([Remainder] string? pageTitle = null)
        {
            Jukebox juke;
            try { juke = GetJukebox; }
            catch
            {
                await ReplyAsync("You must have a song playing when using this command with no parameter.");
                return;
            }

            if (pageTitle == null && !juke.Playing)
            {
                await ReplyAsync("You must have a song playing when using this command with no parameter.");
                return;
            }

            WikiElement info;
            if (pageTitle == null && juke.Playing)
            {
                string? artist = juke.Song!.Value.info.Artist;

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