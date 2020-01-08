using Discord.Commands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CasinoBot.CommandModules
{
    [Group("Bot"), RequireOwner]
    public class BotCommandModule : ModuleBase<SocketCommandContext>
    {
        [Command("Shutdown")]
        public Task ShutdownAsync()
        {
            Environment.Exit(0);
            return Task.CompletedTask;
        }
    }
}