using Discord.Commands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Melodica.Core.CommandModules
{
    [Group("Bot"), RequireOwner]
    public class BotCommandModule : ModuleBase<SocketCommandContext>
    {
        [Command("Shutdown")]
        public Task Shutdown()
        {
            Environment.Exit(0);
            return Task.CompletedTask;
        }
    }
}