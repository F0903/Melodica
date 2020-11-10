using System;
using System.Diagnostics;
using System.Reflection;
using System.Threading.Tasks;

using Discord;
using Discord.Commands;
using Discord.WebSocket;

using Melodica.Services.Logging;
using Melodica.Services.Settings;

namespace Melodica.Core.CommandHandlers
{
    public class SocketCommandHandler : IAsyncCommandHandler
    {
        public SocketCommandHandler(IAsyncLogger logger, GuildSettingsProvider settings)
        {
            this.logger = logger;
            this.settings = settings;
        }

        private readonly IAsyncLogger logger;

        private readonly GuildSettingsProvider settings;

        private DiscordSocketClient? boundClient;

        private CommandService? cmdService;

        async Task OnCommandExecuted(Optional<CommandInfo> info, ICommandContext context, IResult result)
        {
            if (!info.IsSpecified)
                return;

            if (result.Error.HasValue)
            {
                var embed = new EmbedBuilder().WithTitle("**Error!**")
                                              .WithDescription(result.ErrorReason)
                                              .WithCurrentTimestamp()
                                              .WithColor(Color.Red)
                                              .Build();

                await context.Channel.SendMessageAsync(null, false, embed);
            }

#if DEBUG
            await logger.LogAsync(new LogMessage(result.IsSuccess ? LogSeverity.Verbose : LogSeverity.Error, $"{info.Value.Module} - {info.Value.Name} - {context.Guild}", result.IsSuccess ? "Successfully executed command." : result.ErrorReason));
#endif
        }

        async Task OnMessageReceived(SocketMessage message)
        {
            if (!(message is SocketUserMessage msg))
                return;

            if (msg.Author.IsBot)
                return;

            var context = new SocketCommandContext(boundClient, msg);

            int argPos = 0;

            var guildSettings = await settings.GetSettingsAsync(context.Guild.Id);
            string? prefix = guildSettings.Prefix;
            if (!context.Message.HasStringPrefix(prefix, ref argPos))
                return;

            await cmdService!.ExecuteAsync(context, argPos, IoC.Kernel.GetRawKernel());
        }

        public Task HandleCommandsAsync(IDiscordClient client)
        {
            if (!(client is DiscordSocketClient socketClient))
                throw new Exception("This CommandHandler only suppors socket clients.");
            
            boundClient = socketClient;

            socketClient.MessageReceived += OnMessageReceived;

            cmdService = new CommandService(new CommandServiceConfig()
            {
                LogLevel = BotSettings.LogLevel,
                DefaultRunMode = RunMode.Async,
                CaseSensitiveCommands = false
            });

            cmdService.AddModulesAsync(Assembly.GetEntryAssembly(), IoC.Kernel.GetRawKernel());

            cmdService.CommandExecuted += OnCommandExecuted;

            IoC.Kernel.RegisterInstance(cmdService);
            return Task.CompletedTask;
        }
    }
}