using System.Reflection;

using Discord;
using Discord.Commands;
using Discord.Interactions;
using Discord.WebSocket;

using Melodica.Dependencies;
using Melodica.Config;

using Serilog;

namespace Melodica.Core.CommandHandlers;

public class SocketHybridCommandHandler : IAsyncCommandHandler
{
    public SocketHybridCommandHandler(DiscordSocketClient client)
    {
        this.client = client;

        (commands, interactions) = InitializeCommands().Result;
    }

    private readonly IServiceProvider ioc = Dependency.GetServiceProvider();
    private readonly DiscordSocketClient client;
    private readonly CommandService commands;
    private readonly InteractionService interactions;

    readonly record struct CmdContext(IMessageChannel Channel, IGuild Guild);
    readonly record struct CmdInfo(string Module, string Name);
    readonly record struct CmdResult(string Error, bool IsSuccess);

    private Task<CommandService> InitializeTextCommands(Assembly asm)
    {
        var commandService = Dependency.Get<CommandService>();
        commandService.AddModulesAsync(asm, ioc);
        commandService.CommandExecuted += OnTextCommandExecuted;
        
        return Task.FromResult(commandService);
    }

    private async Task<InteractionService> InitializeSlashCommands(Assembly asm)
    {
        var interactionService = Dependency.Get<InteractionService>();
        await interactionService.AddModulesAsync(asm, ioc);
        interactionService.SlashCommandExecuted += OnSlashCommandExecuted;

        if (client.LoginState == LoginState.LoggedIn)
        {
            await RegisterSlashCommands(interactionService);
        }
        else
        {
            client.Ready += () => RegisterSlashCommands(interactionService);
        }
        
        client.InteractionCreated += OnInteractionReceived;

        return interactionService;
    }

    private static async Task RegisterSlashCommands(InteractionService i)
    {
#if DEBUG
        await i.RegisterCommandsToGuildAsync(BotConfig.Settings.SlashCommandDebugGuild);
#else
        await i.RegisterCommandsGloballyAsync();
#endif
    }

    private async Task<(CommandService, InteractionService)> InitializeCommands()
    {
        var asm = Assembly.GetExecutingAssembly();

        var textCmds = InitializeTextCommands(asm);
        var slashCmds = InitializeSlashCommands(asm);

        await Task.WhenAll(textCmds, slashCmds);
        return (textCmds.Result, slashCmds.Result);
    }

    private static Task LogCommandExecution(CmdContext context, CmdInfo info, CmdResult result)
    {
        if (!result.IsSuccess)
        {
            Log.ForContext("CmdModule", info.Module)
            .ForContext("CmdName", info.Name)
            .ForContext("Guild", context.Guild)
            .Error("Command threw and exception\n--->{Error}", result.Error);
            return Task.CompletedTask;
        }

        Log.ForContext("CmdModule", info.Module)
            .ForContext("CmdName", info.Name)
            .ForContext("Guild", context.Guild)
            .Information("Successfully executed command");
        return Task.CompletedTask;
    }

    private async Task OnTextCommandExecuted(Optional<CommandInfo> info, ICommandContext context, Discord.Commands.IResult result)
    {
        var cmdContext = new CmdContext(context.Channel, context.Guild);
        var cmdInfo = new CmdInfo(info.IsSpecified ? info.Value.Module.Name : "Unspecified Module", 
                                  info.IsSpecified ? info.Value.Name : "Unspecified Command");
        var cmdResult = new CmdResult(result.Error.ToString() ?? "Unspecified Error", result.IsSuccess);

        if (result.Error is not null)
        {
            Embed? embed = new EmbedBuilder().WithTitle("**Error!**")
                                          .WithDescription(cmdResult.Error)
                                          .WithCurrentTimestamp()
                                          .WithColor(Color.Red)
                                          .Build();
            await cmdContext.Channel.SendMessageAsync(null, false, embed);
        }

        await LogCommandExecution(cmdContext, cmdInfo, cmdResult);
    }

    private Task OnSlashCommandExecuted(SlashCommandInfo info, IInteractionContext context, Discord.Interactions.IResult result)
    {
        var cmdContext = new CmdContext(context.Channel, context.Guild);
        var cmdInfo = new CmdInfo(info.Module.Name, info.Name);
        var cmdResult = new CmdResult(result.ErrorReason, result.IsSuccess);
        
        return LogCommandExecution(cmdContext, cmdInfo, cmdResult);
    }

    private async Task OnInteractionReceived(SocketInteraction command)
    {
        var ctx = new SocketInteractionContext(client, command);
        var result = await interactions.ExecuteCommandAsync(ctx, ioc);
        if (!result.IsSuccess)
        {
            await command.RespondAsync($"Interaction could not be executed! ```[{result.ErrorReason}]: {(result.Error.HasValue ? result.Error.Value.ToString() : "Unspecified")}```");
            return;
        }
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

        if (!context.Message.HasStringPrefix(BotConfig.Settings.DefaultPrefix, ref argPos))
            return;

        await commands.ExecuteAsync(context, argPos, ioc);
    }
}
