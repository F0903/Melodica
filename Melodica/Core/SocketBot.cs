
using Discord;
using Discord.WebSocket;

using Melodica.Core.CommandHandlers;
using Melodica.Services.Logging;

namespace Melodica.Core;

public class SocketBot<H> where H : IAsyncCommandHandler
{
    public SocketBot(IAsyncLogger logger)
    {
        client = new(new()
        {
            MessageCacheSize = 1,
            LogLevel = BotSettings.LogLevel
        });


        this.logger = logger;
        this.commandHandler = new SocketHybridCommandHandler(logger, client);

        IoC.Kernel.RegisterInstance(client);

        client.Log += this.logger.LogAsync;
        client.MessageReceived += commandHandler.OnMessageReceived;
    }

    private readonly IAsyncLogger logger; 
    private readonly IAsyncCommandHandler commandHandler;
    private readonly DiscordSocketClient client;

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
