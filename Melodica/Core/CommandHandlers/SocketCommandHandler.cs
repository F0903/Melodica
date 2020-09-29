using System;
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

        public bool BuiltCommands { get; private set; } = false;

        private readonly IAsyncLogger logger;

        private readonly GuildSettingsProvider settings;

        private SocketBot? owner;

        private CommandService? cmdService;

        private async Task CommandExecuted(Optional<CommandInfo> info, ICommandContext context, IResult result)
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

            await logger.LogAsync(new LogMessage(result.IsSuccess ? LogSeverity.Verbose : LogSeverity.Error, $"{info.Value.Module} - {info.Value.Name} - {context.Guild}", result.IsSuccess ? "Successfully executed command." : result.ErrorReason));
        }

        public Task BuildCommandsAsync(SocketBot owner)
        {
            this.owner = owner;
            cmdService = new CommandService(new CommandServiceConfig()
            {
                LogLevel = BotSettings.LogLevel,
                DefaultRunMode = RunMode.Async,
                CaseSensitiveCommands = false
            });

            cmdService.AddModulesAsync(Assembly.GetEntryAssembly(), IoC.Kernel.GetRawKernel());

            cmdService.CommandExecuted += CommandExecuted;

            IoC.Kernel.RegisterInstance(cmdService);
            BuiltCommands = true;
            return Task.CompletedTask;
        }

        public async Task HandleCommandsAsync(IMessage message)
        {
            if (!BuiltCommands)
                throw new Exception("You need to call the BuildCommands function before handling them!");

            if (!(message is SocketUserMessage msg))
                return;

            if (msg.Author.IsBot)
                return;

            var context = new SocketCommandContext(owner!.client, msg);

            int argPos = 0;

            var guildSettings = await settings.GetSettingsAsync(context.Guild.Id);
            string? prefix = guildSettings.Prefix;
            if (!context.Message.HasStringPrefix(prefix, ref argPos))
                return;

            await cmdService!.ExecuteAsync(context, argPos, Melodica.IoC.Kernel.GetRawKernel());
        }
    }
}