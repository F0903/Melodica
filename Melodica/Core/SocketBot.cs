using Discord;
using Discord.WebSocket;
using Melodica.Core.CommandHandlers;
using Melodica.Services;
using System.Threading.Tasks;
using Melodica.Services.Logging;

namespace Melodica.Core
{
    public class SocketBot : IBot
    {
        public SocketBot(IAsyncLogger logger, SocketCommandHandler commandHandler)
        {
            this.client = new DiscordSocketClient(new DiscordSocketConfig() 
            { 
                MessageCacheSize = 1,
                LogLevel = BotSettings.LogLevel
            });
            this.logger = logger;
            this.commandHandler = commandHandler;

            Bootstrap().Wait();
        }

        internal readonly DiscordSocketClient client;

        internal readonly SocketCommandHandler commandHandler;

        private readonly IAsyncLogger logger;

        private async Task Bootstrap()
        {
            Melodica.IoC.Kernel.RegisterInstance(client);
      
            await commandHandler.BuildCommandsAsync(this);
            client.MessageReceived += commandHandler.HandleCommandsAsync;
            client.Log += logger.LogAsync;
        }

        public async Task SetActivityAsync(string name, ActivityType type)
        {
            await client.SetActivityAsync(new Game(name, type));
        }

        public async Task ConnectAsync(string activityName, ActivityType type, bool startOnConnect = false)
        {
            await SetActivityAsync(activityName, type);
            await ConnectAsync(startOnConnect);
        }

        public async Task ConnectAsync(bool startOnConnect = false)
        {
            await client.LoginAsync(TokenType.Bot, BotSettings.Token);
            await client.GetApplicationInfoAsync().Result.UpdateAsync();
            if (startOnConnect)
                await client.StartAsync();
        }

        public async Task StartAsync()
        {
            await client.StartAsync();
        }

        public async Task StopAsync()
        {
            await client.StopAsync();
        }
    }
}