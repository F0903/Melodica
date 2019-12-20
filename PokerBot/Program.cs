using System;
using System.Threading.Tasks;
using PokerBot.Core;
using PokerBot.Filehandlers.XML;
using PokerBot.IoC;
using PokerBot;
using PokerBot.Services;
using PokerBot.Services.CommandHandlers;
using PokerBot.Services.Loggers;

namespace PokerBot
{
    public static class Program
    {
        public static readonly IAsyncBot CurrentBot = new SocketBot(PokerBot.Settings.Token, new Discord.WebSocket.DiscordSocketClient(new Discord.WebSocket.DiscordSocketConfig()
        {
            LogLevel = PokerBot.Settings.LogSeverity,           
        }), Kernel.Get<IAsyncLoggingService>(), Kernel.Get<SocketCommandHandler>());

        static async Task Main()
        {
            await CurrentBot.ConnectAsync(true);

            AppDomain.CurrentDomain.ProcessExit += CurrentDomain_ProcessExit;

            await Task.Delay(-1);
        }

        private static void CurrentDomain_ProcessExit(object sender, EventArgs e)
        {
            CurrentBot.StopAsync();
        }
    }
}
