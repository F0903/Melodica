﻿using Discord;
using Discord.WebSocket;
using Melodica.Config;
using Melodica.Core.CommandHandlers;
using Melodica.Dependencies;

namespace Melodica.Core;

public sealed class SocketBot
{
    public SocketBot()
    {
        client = Dependency.Get<DiscordSocketClient>();
        commandHandler = new SocketCommandHandler(client);

        client.Ready += OnReady;
        client.Log += static (msg) =>
        {
            Serilog.Log.Write(msg.Severity.ToLogEventLevel(), "{Source}    {Message}", msg.Source, msg.Message);
            return Task.CompletedTask;
        };
    }

    private readonly DiscordSocketClient client;
    private readonly SocketCommandHandler commandHandler;

    private async Task OnReady() => await commandHandler.InitializeCommandsAsync();

    public async Task SetActivityAsync(string name, ActivityType type) => await client.SetActivityAsync(new Game(name, type));

    public async Task ConnectAsync(string activityName, ActivityType type, bool startOnConnect = false)
    {
        await SetActivityAsync(activityName, type);
        await ConnectAsync(startOnConnect);
    }

    public async Task ConnectAsync(bool startOnConnect = false)
    {
        await client.LoginAsync(TokenType.Bot, BotConfig.Secrets.DiscordToken);
        if (startOnConnect)
            await client.StartAsync();
    }

    public async Task StopAsync() => await client.StopAsync();
}
