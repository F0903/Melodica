using CasinoBot.Core;
using CasinoBot.IoC;
using CasinoBot.Services.CommandHandlers;
using CasinoBot.Services.Loggers;
using System;
using System.Threading.Tasks;

namespace CasinoBot
{
    public static class Program
    {
        public static readonly IAsyncBot CurrentBot = new SocketBot(CasinoBot.Settings.Token, new Discord.WebSocket.DiscordSocketClient(new Discord.WebSocket.DiscordSocketConfig()
        {
            LogLevel = CasinoBot.Settings.LogSeverity,
        }), Kernel.Get<IAsyncLoggingService>(), Kernel.Get<SocketCommandHandler>());

        private static async Task Main()
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