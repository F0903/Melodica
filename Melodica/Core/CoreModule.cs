using Discord;
using Discord.Commands;
using Discord.Interactions;
using Discord.WebSocket;

using Melodica.Dependencies;
using Melodica.Config;

using Microsoft.Extensions.DependencyInjection;

namespace Melodica.Core;

public sealed class CoreModule : DependencyModule
{
    public override IServiceCollection Load() =>
        new ServiceCollection()
        .AddSingleton<DiscordSocketClient>(x => new(new()
        {
            MessageCacheSize = 1,
            LogLevel = BotConfig.Settings.LogLevel.ToLogSeverity()
        }))
        .AddSingleton<InteractionService>(x => new(x.GetRequiredService<DiscordSocketClient>(), new()
        {
            LogLevel = BotConfig.Settings.LogLevel.ToLogSeverity(),
            DefaultRunMode = Discord.Interactions.RunMode.Async,
            UseCompiledLambda = true
        }))
        .AddSingleton<CommandService>(x => new(new()
        {
            LogLevel = BotConfig.Settings.LogLevel.ToLogSeverity(),
            DefaultRunMode = Discord.Commands.RunMode.Async,
            CaseSensitiveCommands = false
        }));
}
