using Discord;
using Discord.WebSocket;
using Suits.Core.Services.CommandHandlers;
using Suits.Core.Services.Loggers;
using System.Threading.Tasks;

namespace Suits.Core
{
    public class SocketBot : IAsyncBot
    {
        public SocketBot(string token, DiscordSocketClient client, IAsyncLoggingService logger, SocketCommandHandler commandHandler)
        {
            this.token = token;
            this.client = client;
            this.logger = logger;
            this.commandHandler = commandHandler;

            Bootstrap().Wait();
        }

        private readonly string token;

        private readonly DiscordSocketClient client;

        internal readonly SocketCommandHandler commandHandler;

        private readonly IAsyncLoggingService logger;

        private async Task Bootstrap()
        {
            Suits.IoC.Kernel.RegisterInstance(client);
      
            await commandHandler.BuildCommandsAsync(client);
            client.MessageReceived += commandHandler.HandleCommandsAsync;
            client.Log += logger.LogAsync;
        }

        public SocketCommandHandler GetCmdHandler() => commandHandler;

        public async Task SetActivityAsync(string name, ActivityType type)
        {
            await client.SetActivityAsync(new Game(name, type));
        }

        public async Task ConnectAsync(bool startOnConnect = false)
        {
            await client.LoginAsync(TokenType.Bot, token);
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