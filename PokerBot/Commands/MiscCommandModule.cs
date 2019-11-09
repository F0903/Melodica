using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Discord.Commands;
using Discord.WebSocket;
using PokerBot.Utility.Extensions;

namespace PokerBot.CommandModules
{
    public class MiscCommandModule : ModuleBase<SocketCommandContext>
    {      
        [Command("Ping")]
        public async Task PingAsync()
        {
            await ReplyAsync("Pong!");
        }

        [Command("Owner")]
        public async Task GetOwnerAsync()
        {
            await ReplyAsync(Context.Message.Author.IsOwnerOfApp() ? "You already know..." : $"My owner is {Utility.Utility.GetAppOwnerAsync()}");
        }

        [Group("Debug")]
        public class DebugCommands : ModuleBase<SocketCommandContext>
        {
            [Command("ThrowException"), Alias("Throw"), RequireUserPermission(Discord.GuildPermission.Administrator)]
            public Task ThrowExceptionAsync() =>
                throw new Exception("Test exception.");

            [Command("GetTokenType")]
            public Task GetTokenTypeAsync() =>
                ReplyAsync($"Context Client: {Context.Client.TokenType.ToString()}\nDI Client: {IoC.Kernel.Get<DiscordSocketClient>().TokenType.ToString()}");
        }
    }
}
