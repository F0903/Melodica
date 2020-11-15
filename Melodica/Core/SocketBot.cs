using System.Threading.Tasks;

using Discord;
using Discord.WebSocket;

using Melodica.Core.CommandHandlers;
using Melodica.Services.Logging;

namespace Melodica.Core
{
    public class SocketBot : IBot
    {
        public SocketBot(IAsyncLogger logger, SocketCommandHandler commandHandler)
        {
            client = new DiscordSocketClient(new DiscordSocketConfig()
            {
                MessageCacheSize = 1,
                LogLevel = BotSettings.LogLevel
            });

            this.logger = logger;
            this.commandHandler = commandHandler;

            Bootstrap().Wait();
        }

        private readonly DiscordSocketClient client;

        private readonly IAsyncLogger logger;
        private readonly SocketCommandHandler commandHandler;

        private async Task Bootstrap()
        {
            IoC.Kernel.RegisterInstance(client);

            await commandHandler.HandleCommandsAsync(client);

            client.Log += logger.LogAsync;
        }

        public async Task SetActivityAsync(string name, ActivityType type) => await client.SetActivityAsync(new Game(name, type));

        public async Task ConnectAsync(string activityName, ActivityType type, bool startOnConnect = false)
        {
            await SetActivityAsync(activityName, type);
            await ConnectAsync(startOnConnect);
        }

        public async Task ConnectAsync(bool startOnConnect = false)
        {
            await client.LoginAsync(TokenType.Bot, BotSettings.Token);
            if (startOnConnect)
                await client.StartAsync();
        }

        public async Task StartAsync() => await client.StartAsync();

        public async Task StopAsync() => await client.StopAsync();
    }
}