using Suits.Core;
using Suits.IoC;
using Suits.Core.Services.CommandHandlers;
using Suits.Core.Services.Loggers;
using System;
using System.Threading.Tasks;

namespace Suits
{
    public static class Program
    {
        private static readonly SocketBot currentBot = new SocketBot(Suits.Settings.Token, new Discord.WebSocket.DiscordSocketClient(new Discord.WebSocket.DiscordSocketConfig()
        {
            LogLevel = Suits.Settings.LogSeverity,
        }), Kernel.Get<IAsyncLoggingService>(), Kernel.Get<SocketCommandHandler>());

        private static async Task Main()
        {
            await currentBot.ConnectAsync(true);
            await currentBot.SetActivityAsync($"{Settings.Prefix}play", Discord.ActivityType.Listening);
            AppDomain.CurrentDomain.ProcessExit += CurrentDomain_ProcessExit;

            await Task.Delay(-1);
        }

        private static void CurrentDomain_ProcessExit(object sender, EventArgs e)
        {
            currentBot.StopAsync().Wait();
        }
    }
}