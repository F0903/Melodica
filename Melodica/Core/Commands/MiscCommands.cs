using Discord;
using Discord.Commands;
using Discord.WebSocket;

using Melodica.Utility.Extensions;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Melodica.Core.Commands
{
    [Name("Misc")]
    public class MiscCommands : ModuleBase<SocketCommandContext>
    {
        public MiscCommands(CommandService cmService)
        {
            this.commandService = cmService;
        }

        private readonly CommandService commandService;

        private static List<CommandInfo>? cachedCommandInfo;

        [Command("Help"), Summary("Prints out all available commands.")]
        public async Task HelpAsync(int page = 0)
        {
            if (page < 0)
            {
                await ReplyAsync("Page number cannot be less than 0");
                return;
            }

            if (cachedCommandInfo == null)
            {
                var modules = commandService.Modules.ToArray().OrderBy(x => x.Group);
                cachedCommandInfo = new List<CommandInfo>();
                foreach (var module in modules)
                    cachedCommandInfo.AddRange(module.Commands);
            }

            var maxElemsPerPage = 20;
            var totalPages = cachedCommandInfo.Count / maxElemsPerPage;

            if(page > totalPages)
            {
                await ReplyAsync($"Page number cannot be more than {totalPages}");
                return;
            }

            var fields = new List<EmbedFieldBuilder>(cachedCommandInfo.Count);

            var startIndx = page * maxElemsPerPage;
            var endIndx = (cachedCommandInfo.Count - startIndx) <= maxElemsPerPage ? cachedCommandInfo.Count : startIndx + maxElemsPerPage;
            for (int i = startIndx; i < endIndx; i++)
            {
                var command = cachedCommandInfo[i];
                var commandAliases = command.Aliases;
                fields.Add(new EmbedFieldBuilder() 
                { 
                    IsInline = false, 
                    Name = $"**{command.Name}**, { commandAliases.ToArray().SeperateStrings() }", 
                    Value = !string.IsNullOrEmpty(command.Summary) ? command.Summary : "No summary." 
                });
            }

            await ReplyAsync(null, false, new EmbedBuilder()
            {
                Title = "Commands",
                Fields = fields,
                Footer = new EmbedFooterBuilder()
                {
                    Text = $"{page}/{totalPages}"
                },
            }.Build());
        }

        [Command("Owner"), Summary("Gets the owner of the bot.")]
        public async Task GetOwnerAsync()
        {
            var owner = await Utility.Utils.GetAppOwnerAsync();
            await ReplyAsync(Context.Message.Author.Id == owner.Id ? "You already know..." : $"My owner is {owner}");
        }
    }
}