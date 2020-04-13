using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Suits.Utility.Extensions;
using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Suits.Core.CommandModules
{
    [Name("Misc")]
    public class MiscCommandModule : ModuleBase<SocketCommandContext>
    {
        public MiscCommandModule(CommandService cmService)
        {
            this.commandService = cmService;
        }

        private readonly CommandService commandService;

        [Command("TTS"), RequireUserPermission(GuildPermission.Administrator)]
        public async Task TTSAsync([Remainder] string msg)
        {
            var message = await ReplyAsync(msg, true);
            await message.DeleteAsync();
            await Context.Message.DeleteAsync();
        }

        [Command("Spam"), RequireOwner]
        public async Task SpamAsync(string user, [Remainder] string msg)
        {
            var dm = await Context.Guild.AutoGetUser(user).GetOrCreateDMChannelAsync();
            for (int i = 0; i < 3; i++)
            {
                await Task.Delay(1);
                await dm.SendMessageAsync(msg);
            }
        }

        [Command("Help"), Summary("Prints out all available commands.")]
        public async Task HelpAsync(int page = 1)
        {
            if (page < 1) page = 1;

            var mods = commandService.Modules.ToArray();

            int elemsPerPage = 25;

            int CalcPages()
            {
                float i = 0;
                i += mods.Aggregate(0, (val, x) => val += x.Commands.Count);
                i /= elemsPerPage;
                return i > (int)i ? (int)++i : (int)i;
            }
           
            var eb = new EmbedBuilder().WithTitle($"Commands [{page} of {CalcPages()}]");           
            for(int x = 0; x < (mods.Length > elemsPerPage ? elemsPerPage : mods.Length); x++)
            {
                var comms = mods[x].Commands;
                for (int y = (page - 1) * elemsPerPage; y < comms.Count; y++)
                {
                    eb.AddField($"{comms[y].Module.Name} | **{comms[y].Name}** [{comms[y].Aliases.Unfold(',')}] ({comms[y].Parameters.Unfold(',') ?? ""})", string.IsNullOrEmpty(comms[y].Summary) ? "No summary." : comms[y].Summary);
                }           
            };          
            await ReplyAsync(null, false, eb.Build());
        }       

        [Command("Owner")]
        public async Task GetOwnerAsync()
        {
            var owner = await Utility.General.GetAppOwnerAsync();
            await ReplyAsync(Context.Message.Author.Id == owner.Id ? "You already know..." : $"My owner is {owner}");
        }
    }
}