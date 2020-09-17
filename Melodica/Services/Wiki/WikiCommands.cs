using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using Discord;
using Discord.Commands;

using Melodica.Services.Playback;
using Melodica.Utility.Extensions;

namespace Melodica.Services.Wiki
{
    public class WikiCommands : ModuleBase<SocketCommandContext>
    {
        public WikiCommands(WikiProvider wiki, JukeboxProvider jukebox)
        {
            this.wiki = wiki;
            this.jukeboxProvider = jukebox;
        }

        readonly WikiProvider wiki;
        readonly JukeboxProvider jukeboxProvider;
        Jukebox Jukebox => jukeboxProvider.GetJukeboxAsync(Context.Guild).Result;

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
                var songTitle = Jukebox.GetSong().info.Title.ExtractArtistName();
                
                info = await wiki.GetInfoAsync(songTitle);
            }
            else info = await wiki.GetInfoAsync(pageTitle!);

            await ReplyAsync(null, false, new EmbedBuilder()
            {
                ThumbnailUrl = info.ImageUrl,
                Title = info.Title,
                Description = info.Info
            }.Build());
        }
    }
}
