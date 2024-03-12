using System.Reflection;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Melodica.Config;
using Melodica.Dependencies;
using Melodica.Utility;
using Serilog;

namespace Melodica.Core.CommandHandlers;

public sealed class SocketCommandHandler(DiscordSocketClient client) : IAsyncCommandHandler
{
    private readonly IServiceProvider ioc = Dependency.GetServiceProvider();
    private InteractionService? interactions;

    private async Task<InteractionService> InitializeSlashCommandsAsync(Assembly asm)
    {
        var interactionService = Dependency.Get<InteractionService>();
        var modules = await interactionService.AddModulesAsync(asm, ioc);
        foreach (var mod in modules)
        {
            Log.Debug($"Added interaction module: {mod.Name}");
        }
        interactionService.SlashCommandExecuted += OnSlashCommandExecutedAsync;

        try
        {
            await RegisterSlashCommandsAsync(interactionService);
        }
        catch (Exception ex)
        {
            Log.Error($"Command registration threw an error: {ex}");
        }

        client.InteractionCreated += OnInteractionReceived;

        return interactionService;
    }

    private static async Task RegisterSlashCommandsAsync(InteractionService i)
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

    public async Task InitializeCommandsAsync()
    {
        var asm = Assembly.GetExecutingAssembly();
        interactions = await InitializeSlashCommandsAsync(asm);
    }

    private static ValueTask<Embed> BuildErrorEmbedAsync(string errorDesc)
    {
        var embed = new EmbedBuilder
        {
            Color = Color.DarkRed,
            Title = "Error!",
            Description = $"A command threw an error:\n```{errorDesc}```",
        }.Build();
        return embed.WrapValueTask();
    }

    private async Task OnSlashCommandExecutedAsync(SlashCommandInfo info, IInteractionContext context, IResult result)
    {
        if (!result.IsSuccess)
        {
            Log.ForContext("CmdModule", info.Module)
            .ForContext("CmdName", info.Name)
            .ForContext("Guild", context.Guild)
            .Error("Command threw an exception {Error}", $"{info.Name} => {result.ErrorReason}");

            var errorEmbed = await BuildErrorEmbedAsync(result.ErrorReason);
            if (context.Interaction.HasResponded)
            {
                await context.Interaction.ModifyOriginalResponseAsync(x => x.Embed = errorEmbed);
                return;
            }

            await context.Interaction.RespondAsync(embed: errorEmbed);
            return;
        }

        Log.ForContext("CmdModule", info.Module)
            .ForContext("CmdName", info.Name)
            .ForContext("Guild", context.Guild)
            .Information("Successfully executed command");
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
