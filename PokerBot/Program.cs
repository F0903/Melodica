using System;
using System.Threading.Tasks;
using PokerBot.Core;
using PokerBot.Filehandlers.XML;
using PokerBot.IoC;
using PokerBot.Services;

namespace PokerBot
{
    public class Program
    {
        private static readonly IAsyncBot bot = new SocketBot(Settings.Token, new Discord.WebSocket.DiscordSocketClient(new Discord.WebSocket.DiscordSocketConfig()
        {
            LogLevel = Settings.LogSeverity,           
        }), Kernel.Get<IAsyncLogger>(), Kernel.Get<IAsyncCommandHandler>());

        static async Task Main()
        {
            await bot.ConnectAsync(true);

            AppDomain.CurrentDomain.ProcessExit += CurrentDomain_ProcessExit;

            await Task.Delay(-1);
        }

        private static void CurrentDomain_ProcessExit(object sender, EventArgs e)
        {
            bot.StopAsync();
        }
    }
}
