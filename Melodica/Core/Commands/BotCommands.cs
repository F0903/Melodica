using System;
using System.Threading.Tasks;

using Discord.Commands;

namespace Melodica.Core.Commands
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