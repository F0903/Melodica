using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Discord.Commands;

namespace PokerBot.Commands
{
    public class MiscCommands : ModuleBase<SocketCommandContext>
    {      
        [Command("Ping")]
        public async Task PingAsync()
        {
            await ReplyAsync("Pong!");
        }

        [Group("Debug")]
        public class DebugCommands : ModuleBase<SocketCommandContext>
        {
            [Command("ThrowException"), RequireUserPermission(Discord.GuildPermission.Administrator)]
            public Task ThrowException() =>
                throw new Exception("Test exception.");
        }
    }
}
