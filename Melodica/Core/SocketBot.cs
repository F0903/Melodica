
using Discord;
using Discord.WebSocket;

using Melodica.Config;
using Melodica.Core.CommandHandlers;

namespace Melodica.Core;

public class SocketBot<H> where H : IAsyncCommandHandler
{
    public SocketBot()
    {
        client = new(new()
        {
            MessageCacheSize = 1,
            LogLevel = BotConfig.Settings.LogLevel.ToLogSeverity()
        });

        IoC.Kernel.RegisterInstance(client);

        this.commandHandler = IoC.Kernel.Get<H>();

        client.MessageReceived += commandHandler.OnMessageReceived;
        client.Log += static (msg) =>
        {
            Serilog.Log.Write(msg.Severity.ToLogEventLevel(), "{Source}    {Message}", msg.Source, msg.Message);
            return Task.CompletedTask;
        };
    }

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
        await client.LoginAsync(TokenType.Bot, BotConfig.Secrets.DiscordToken);
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
