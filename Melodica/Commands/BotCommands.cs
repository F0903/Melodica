using Discord.Commands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Melodica.Commands
{
    [Group("Bot"), RequireOwner]
    public class BotCommands : ModuleBase<SocketCommandContext>
    {
        [Command("Shutdown")]
        public Task Shutdown()
        {
            Environment.Exit(0);
            return Task.CompletedTask;
        }
    }
}