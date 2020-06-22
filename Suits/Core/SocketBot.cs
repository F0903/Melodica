using Discord;
using Discord.WebSocket;
using Suits.Core.Services.CommandHandlers;
using Suits.Core.Services;
using System.Threading.Tasks;

namespace Suits.Core
{
    public class SocketBot : IAsyncBot
    {
        public SocketBot(BotSettings settings, IAsyncLoggingService logger, SocketCommandHandler commandHandler)
        {
            this.settings = settings;
            this.client = new DiscordSocketClient(new DiscordSocketConfig() 
            { 
                MessageCacheSize = 1,
                LogLevel = settings.LogSeverity
            });
            this.logger = logger;
            this.commandHandler = commandHandler;

            Bootstrap().Wait();
        }

        internal readonly BotSettings settings;

        internal readonly DiscordSocketClient client;

        internal readonly SocketCommandHandler commandHandler;

        private readonly IAsyncLoggingService logger;

        private async Task Bootstrap()
        {
            Suits.IoC.Kernel.RegisterInstance(client);
      
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
            await client.LoginAsync(TokenType.Bot, settings.Token);
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