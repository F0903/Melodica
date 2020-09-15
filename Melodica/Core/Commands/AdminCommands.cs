﻿using Discord;
using Discord.Commands;
using Melodica.Core;
using Melodica.Utility.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Melodica.Core.Commands
{
    [Group("Admin"), RequireUserPermission(GuildPermission.Administrator, ErrorMessage = "This command can only be used by guild admins.")]
    public class AdminCommands : ModuleBase<SocketCommandContext>
    {
        [Command("Message"), Summary("Messages a guild user by username.")]
        public async Task MessageGuildUserAsync(string userToMsg, [Remainder] string content) =>
            await Context.Guild.AutoGetUser(userToMsg).GetOrCreateDMChannelAsync().Result.SendMessageAsync(content); 
        
        [Command("Prefix"), Summary("Changes the prefix.")]
        public async Task ChangePrefixAsync(string newPrefix)
        {
            var settings = GuildSettings.Get(Context.Guild);
            settings.Prefix = newPrefix;
            settings.SaveData();
            await ReplyAsync($"Prefix for {Context.Guild.Name} is now {newPrefix}");
        }
    }
}