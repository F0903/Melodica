﻿using Discord;
using Discord.Commands;

using Melodica.Core;
using Melodica.Services.Settings;
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
        public AdminCommands(GuildSettingsProvider settings)
        {
            this.settings = settings;
        }

        readonly GuildSettingsProvider settings;

        [Command("Message"), Summary("Messages a guild user by username.")]
        public async Task MessageGuildUserAsync(string userToMsg, [Remainder] string content) =>
            await Context.Guild.AutoGetUser(userToMsg).GetOrCreateDMChannelAsync().Result.SendMessageAsync(content);

        [Command("Prefix"), Summary("Changes the prefix.")]
        public async Task ChangePrefixAsync(string newPrefix)
        {
            var status = await settings.UpdateSettingsAsync(Context.Guild.Id, x =>
            {
                x.Prefix = newPrefix;
                return x;
            });
            if (status == 1)
                await ReplyAsync(null, false, new EmbedBuilder()
                {
                    Description = $"Succesfully changed prefix in **{Context.Guild.Name}** to {newPrefix}",
                    Color = Color.Green
                }.Build());
            else
                await ReplyAsync(null, false, new EmbedBuilder()
                {
                    Description = $"Could not change prefix",
                    Color = Color.Red
                }.Build());
        }
    }
}