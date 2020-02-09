using Discord;
using Discord.Commands;
using Suits.Utility.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Suits.Core.CommandModules
{
    [Group("Admin"), RequireUserPermission(GuildPermission.Administrator, ErrorMessage = "This command can only be used by guild admins.")]
    public class AdminCommandModule : ModuleBase<SocketCommandContext>
    {
        [Command("Message"), Summary("Messages a guild user by username.")]
        public async Task MessageGuildUserAsync(string userToMsg, [Remainder] string content) =>
            await Context.Guild.AutoGetUser(userToMsg).GetOrCreateDMChannelAsync().Result.SendMessageAsync(content);       
    }
}