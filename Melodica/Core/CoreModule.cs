using Discord;
using Discord.Commands;
using Discord.Interactions;
using Discord.WebSocket;

using Melodica.Dependencies;

using Microsoft.Extensions.DependencyInjection;

namespace Melodica.Core;

public class CoreModule : DependencyModule
{
    public override IServiceCollection Load() =>
        new ServiceCollection()
        .AddSingleton<DiscordSocketClient>(x => new(new()
        {
            MessageCacheSize = 1,
            LogLevel = BotSettings.LogLevel
        }))
        .AddSingleton<InteractionService>(x => new(x.GetRequiredService<DiscordSocketClient>(), new()
        {
            LogLevel = BotSettings.LogLevel,
            DefaultRunMode = Discord.Interactions.RunMode.Async,
            UseCompiledLambda = true
        }))
        .AddSingleton<CommandService>(x => new(new()
        {
            LogLevel = BotSettings.LogLevel,
            DefaultRunMode = Discord.Commands.RunMode.Async,
            CaseSensitiveCommands = false
        }));
}
