
using Discord;
using Discord.WebSocket;

using Melodica.Core.CommandHandlers;
using Melodica.Services.Logging;
using Melodica.Dependencies;

namespace Melodica.Core;

public class SocketBot
{
    public SocketBot(IAsyncLogger logger)
    {
        this.client = Dependency.Get<DiscordSocketClient>();
        this.logger = logger;
        this.commandHandler = new SocketHybridCommandHandler(logger, client);

        client.Log += this.logger.LogAsync;
        client.MessageReceived += this.commandHandler.OnMessageReceived;

        System.Diagnostics.Process.GetCurrentProcess().PriorityClass = BotSettings.ProcessPriority;
    }

    private readonly DiscordSocketClient client;
    private readonly IAsyncLogger logger; 
    private readonly IAsyncCommandHandler commandHandler;

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
        await client.LoginAsync(TokenType.Bot, BotSecrets.DiscordToken);
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
