using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;

namespace PokerBot.Commands
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
