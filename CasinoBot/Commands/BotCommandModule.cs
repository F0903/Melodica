using Discord.Commands;
using System;
using System.Threading.Tasks;

namespace CasinoBot.Commands
{
    [Group("Bot")]
    public class BotCommandModule : ModuleBase<SocketCommandContext>
    {
        [Command("Shutdown"), RequireOwner]
        public Task ShutdownAsync()
        {
            Environment.Exit(0);
            return Task.CompletedTask;
        }
    }
}