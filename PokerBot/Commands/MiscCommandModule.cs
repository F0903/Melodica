using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Discord.Commands;

namespace PokerBot.CommandModules
{
    public class MiscCommandModule : ModuleBase<SocketCommandContext>
    {      
        [Command("Ping")]
        public async Task PingAsync()
        {
            await ReplyAsync("Pong!");
        }

        [Group("Debug")]
        public class DebugCommands : ModuleBase<SocketCommandContext>
        {
            [Command("ThrowException"), Alias("Throw"), RequireUserPermission(Discord.GuildPermission.Administrator)]
            public Task ThrowExceptionAsync() =>
                throw new Exception("Test exception.");
        }
    }
}
