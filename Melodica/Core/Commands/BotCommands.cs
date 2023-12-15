using Discord.Commands;

namespace Melodica.Core.Commands;

[Group("Bot"), RequireOwner]
public sealed class BotCommands : ModuleBase<SocketCommandContext>
{
    [Command("Shutdown")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Cannot be static due to Discord library.")]
    public Task Shutdown()
    {
        Environment.Exit(0);
        return Task.CompletedTask;
    }
}
