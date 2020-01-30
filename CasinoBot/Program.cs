using CasinoBot.Core;
using CasinoBot.IoC;
using CasinoBot.Core.Services.CommandHandlers;
using CasinoBot.Core.Services.Loggers;
using System;
using System.Threading.Tasks;

namespace CasinoBot
{
    public static class Program
    {
        public static readonly SocketBot CurrentBot = new SocketBot(CasinoBot.Settings.Token, new Discord.WebSocket.DiscordSocketClient(new Discord.WebSocket.DiscordSocketConfig()
        {
            LogLevel = CasinoBot.Settings.LogSeverity,
        }), Kernel.Get<IAsyncLoggingService>(), Kernel.Get<SocketCommandHandler>());

        private static async Task Main()
        {
            await CurrentBot.ConnectAsync(true);
            await CurrentBot.SetActivityAsync($"{Settings.Prefix}play", Discord.ActivityType.Listening);
            AppDomain.CurrentDomain.ProcessExit += CurrentDomain_ProcessExit;

            await Task.Delay(-1);
        }

        private static void CurrentDomain_ProcessExit(object sender, EventArgs e)
        {
            CurrentBot.StopAsync().Wait();
        }
    }
}