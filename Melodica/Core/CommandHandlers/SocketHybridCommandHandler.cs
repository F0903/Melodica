using System.Reflection;

using Discord;
using Discord.Commands;
using Discord.WebSocket;

using Melodica.Services.Logging;
using Melodica.Services.Settings;

namespace Melodica.Core.CommandHandlers;

public class SocketHybridCommandHandler : IAsyncCommandHandler
{
    public SocketHybridCommandHandler(IAsyncLogger logger, DiscordSocketClient client)
    {
        this.logger = logger;
        this.client = client;

        cmdService = new(new()
        {
            LogLevel = BotSettings.LogLevel,
            DefaultRunMode = RunMode.Async,
            CaseSensitiveCommands = false
        });
        cmdService.AddModulesAsync(Assembly.GetEntryAssembly(), IoC.Kernel.GetRawKernel());
        cmdService.CommandExecuted += OnCommandExecuted;
        IoC.Kernel.RegisterInstance(cmdService);
    }

    private readonly IAsyncLogger logger;
    private readonly DiscordSocketClient client;
    private readonly CommandService cmdService;

    private async Task OnCommandExecuted(Optional<CommandInfo> info, ICommandContext context, IResult result)
    {
        if (!info.IsSpecified)
            return;

        if (result.Error.HasValue)
        {
            Embed? embed = new EmbedBuilder().WithTitle("**Error!**")
                                          .WithDescription(result.ErrorReason)
                                          .WithCurrentTimestamp()
                                          .WithColor(Color.Red)
                                          .Build();

            await context.Channel.SendMessageAsync(null, false, embed);
        }

        await logger.LogAsync(new LogMessage(result.IsSuccess ? LogSeverity.Verbose : LogSeverity.Error, $"{info.Value.Module} - {info.Value.Name} - {context.Guild}", result.IsSuccess ? "Successfully executed command." : result.ErrorReason));
    }

    public async Task OnMessageReceived(IMessage message)
    {
        if (message is not SocketUserMessage msg)
            return;

        if (msg.Channel is IDMChannel)
            return;

        if (msg.Author.IsBot)
            return;

        SocketCommandContext? context = new(client, msg);

        int argPos = 0;

        GuildSettingsInfo? guildSettings = await GuildSettings.GetSettingsAsync(context.Guild.Id);
        string? prefix = guildSettings.Prefix;
        if (!context.Message.HasStringPrefix(prefix, ref argPos))
            return;

        await cmdService!.ExecuteAsync(context, argPos, IoC.Kernel.GetRawKernel());
    }
}
