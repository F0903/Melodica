using Discord;
using Discord.WebSocket;
using CasinoBot.Services.CommandHandlers;
using CasinoBot.Services.Loggers;
using System.Threading.Tasks;

namespace CasinoBot.Core
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

        private readonly SocketCommandHandler commandHandler;

        private readonly IAsyncLoggingService logger;

        private async Task Bootstrap()
        {
            CasinoBot.IoC.Kernel.RegisterInstance(client);

            await client.SetActivityAsync(new Game($"{Settings.Prefix}play", ActivityType.Listening));

            await commandHandler.BuildCommandsAsync(client);
            client.MessageReceived += commandHandler.HandleCommandsAsync;
            client.Log += logger.LogAsync;
        }

        public SocketCommandHandler GetCmdHandler() => commandHandler;

        public async Task ConnectAsync(bool startOnConnect = false)
        {
            await client.LoginAsync(TokenType.Bot, token);
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