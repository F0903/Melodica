using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Suits.Core.Services;
using Suits.Utility.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace Suits.Core.Services.CommandHandlers
{
    public class SocketCommandHandler : IAsyncCommandHandlerService
    {
        public SocketCommandHandler(IAsyncLoggingService logger)
        {
            this.logger = logger;           
        }

        public bool BuiltCommands { get; private set; } = false;

        private readonly IAsyncLoggingService logger;

        private SocketBot? owner;

        private CommandService? cmdService;

        private async Task CommandExecuted(Optional<CommandInfo> info, ICommandContext context, IResult result)
        {
            if (!info.IsSpecified)
                return;

            LogMessage msg = new LogMessage(LogSeverity.Info, $"Command Execution - {info.Value.Module} - {info.Value.Name}", "Command executed successfully.");
            
            if (result.Error.HasValue)
            {
                msg = new LogMessage(
                        LogSeverity.Error,
                        $"Command Execution - {info.Value.Module} - {info.Value.Name}",
                        $"Error: {result.ErrorReason} Exception type: {(result.Error.HasValue ? result.Error.Value.ToString() : "not specified")}");

                await context.Channel.SendMessageAsync($"**Error:** {result.ErrorReason}{(owner!.settings.LogSeverity == LogSeverity.Debug ? $"\n**Type:** {result.Error.Value}" : string.Empty)}");
            }

            await logger.LogAsync(msg);
        }

        public Task BuildCommandsAsync(SocketBot owner)
        {
            this.owner = owner;
            cmdService = new CommandService(new CommandServiceConfig()
            {
                LogLevel = owner.settings.LogSeverity,
                DefaultRunMode = RunMode.Async,
                CaseSensitiveCommands = false
            });

            cmdService.AddModulesAsync(Assembly.GetAssembly(typeof(SocketCommandHandler)), IoC.Kernel.GetRawKernel());

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

            if (!context.Message.HasStringPrefix(GuildSettings.GetOrCreateSettings(context.Guild, () => new GuildSettings(context.Guild)).Prefix, ref argPos))
                return;

            await cmdService!.ExecuteAsync(context, argPos, Suits.IoC.Kernel.GetRawKernel());
        }
    }
}