using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using System.Reflection;
using System.Threading.Tasks;
using Discord;
using Discord.Net;
using Discord.Commands;
using Discord.WebSocket;
using PokerBot.Services;
using PokerBot.Services.Loggers;
using PokerBot.Services.CommandHandlers;
using PokerBot.Modules;

namespace PokerBot.Core
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
            PokerBot.IoC.Kernel.RegisterInstance(client);

            // DO NOT
            //await ModuleLoader.LoadModulesAsync(commandHandler, logger);

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
