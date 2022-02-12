using Discord.Interactions;
using Discord.WebSocket;

namespace Melodica.Core.Commands;

public class TestSlashCommands : InteractionModuleBase
{
    [SlashCommand("ping", "Pong!")]
    public async Task Ping()
    {
        await RespondAsync("Pong!");
    }

    [SlashCommand("echo", "Echoes message.")]
    public async Task Echo(string message)
    {
        await RespondAsync(message);
    }
}
