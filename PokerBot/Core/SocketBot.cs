﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Net;
using Discord.Commands;
using Discord.WebSocket;
using PokerBot.Services;

namespace PokerBot.Core
{
    public class SocketBot : IAsyncBot
    {
        public SocketBot(string token, DiscordSocketClient client, IAsyncLoggingService logger, IAsyncCommandHandlerService commandHandler)
        {
            this.token = token;
            this.client = client;
            this.logger = logger;
            this.commandHandler = commandHandler;           

            Bootstrap().Wait();
        }

        private readonly string token;

        private readonly DiscordSocketClient client;

        private readonly IAsyncCommandHandlerService commandHandler;

        private readonly IAsyncLoggingService logger;       

        private Task Bootstrap()
        {
            IoC.Kernel.RegisterInstance(client);

            commandHandler.BuildCommandsAsync(client);
            client.MessageReceived += commandHandler.HandleCommandsAsync;
            client.Log += logger.LogAsync;
            return Task.CompletedTask;
        }
         
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
