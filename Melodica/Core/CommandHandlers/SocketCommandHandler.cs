using System.Reflection;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Melodica.Config;
using Melodica.Dependencies;
using Serilog;

namespace Melodica.Core.CommandHandlers;

public sealed class SocketCommandHandler(DiscordSocketClient client) : IAsyncCommandHandler
{
    private readonly IServiceProvider ioc = Dependency.GetServiceProvider();
    private InteractionService? interactions;

    private async Task<InteractionService> InitializeSlashCommands(Assembly asm)
    {
        var interactionService = Dependency.Get<InteractionService>();
        var modules = await interactionService.AddModulesAsync(asm, ioc);
        foreach (var mod in modules)
        {
            Log.Debug($"Added interaction module: {mod.Name}");
        }
        interactionService.SlashCommandExecuted += OnSlashCommandExecuted;

        try
        {
            await RegisterSlashCommands(interactionService);
        }
        catch (Exception ex)
        {
            Log.Error($"Command registration threw an error: {ex}");
        }

        client.InteractionCreated += OnInteractionReceived;

        return interactionService;
    }

    private static async Task RegisterSlashCommands(InteractionService i)
    {
        IReadOnlyCollection<IApplicationCommand> registeredCommands;
#if DEBUG
        Log.Debug("Registering commands to debug guild.");
        registeredCommands = await i.RegisterCommandsToGuildAsync(BotConfig.Settings.SlashCommandDebugGuild);
#else
        Log.Debug("Registering commands globally.");
        registeredCommands = await i.RegisterCommandsGloballyAsync();
#endif
        foreach (var cmd in registeredCommands)
        {
            Log.Debug($"Registered command: {cmd.Name}");
        }
    }

    public async Task InitializeCommands()
    {
        var asm = Assembly.GetExecutingAssembly();
        interactions = await InitializeSlashCommands(asm);
    }

    private static ValueTask<Embed> BuildErrorEmbed(string errorDesc)
    {
        var embed = new EmbedBuilder
        {
            Color = Color.DarkRed,
            Title = "Error!",
            Description = $"A command threw an error:\n```{errorDesc}```",
        }.Build();
        return ValueTask.FromResult(embed);
    }

    private Task OnSlashCommandExecuted(SlashCommandInfo info, IInteractionContext context, IResult result)
    {
        if (!result.IsSuccess)
        {
            Log.ForContext("CmdModule", info.Module)
            .ForContext("CmdName", info.Name)
            .ForContext("Guild", context.Guild)
            .Error("Command threw an exception {Error}", $"{info.Name} => {result.ErrorReason}");

            context.Interaction.ModifyOriginalResponseAsync(async x => x.Embed = await BuildErrorEmbed(result.ErrorReason));
            return Task.CompletedTask;
        }

        Log.ForContext("CmdModule", info.Module)
            .ForContext("CmdName", info.Name)
            .ForContext("Guild", context.Guild)
            .Information("Successfully executed command");
        return Task.CompletedTask;
    }

    private async Task OnInteractionReceived(SocketInteraction command)
    {
        if (interactions is null) return;

        SocketInteractionContext ctx = new(client, command);
        var result = await interactions.ExecuteCommandAsync(ctx, ioc);
        if (!result.IsSuccess)
        {
            await command.RespondAsync($"Interaction could not be executed! ```[{result.ErrorReason}]: {(result.Error.HasValue ? result.Error.Value.ToString() : "Unspecified")}```");
            return;
        }
    }
}
