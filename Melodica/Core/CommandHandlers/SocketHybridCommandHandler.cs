using System.Reflection;

using Discord;
using Discord.Commands;
using Discord.WebSocket;

using Melodica.Config;
using Melodica.Services.Settings;

using Serilog;

namespace Melodica.Core.CommandHandlers;

public class SocketHybridCommandHandler : IAsyncCommandHandler
{
    public SocketHybridCommandHandler(DiscordSocketClient client)
    {
        this.client = client;

        cmdService = new(new()
        {
            LogLevel = BotConfig.Settings.LogLevel.ToLogSeverity(),
            DefaultRunMode = RunMode.Async,
            CaseSensitiveCommands = false
        });
        cmdService.AddModulesAsync(Assembly.GetEntryAssembly(), IoC.Kernel.GetRawKernel());
        cmdService.CommandExecuted += OnCommandExecuted;
        IoC.Kernel.RegisterInstance(cmdService);
    }

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

        if (!result.IsSuccess)
        {
            Log.ForContext("CmdModule", info.Value.Module)
                .ForContext("CmdName", info.Value.Name)
                .ForContext("Guild", context.Guild)
                .Error("Command did not execute successfully\n---> {Error}", result.ErrorReason);
        }

        Log.ForContext("CmdModule", info.Value.Module)
                .ForContext("CmdName", info.Value.Name)
                .ForContext("Guild", context.Guild)
                .Information("Command executed successfully");
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
