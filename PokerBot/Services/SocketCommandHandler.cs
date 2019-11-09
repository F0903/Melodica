using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using PokerBot.Core;
using PokerBot.Utility.Extensions;

namespace PokerBot.Services
{
    public class SocketCommandHandler : IAsyncCommandHandlerService
    {
        public SocketCommandHandler(IAsyncLoggingService logger)
        {
            this.logger = logger;
        }

        public bool BuiltCommands { get; private set; } = false;

        private readonly IAsyncLoggingService logger;

        private DiscordSocketClient client;

        private CommandService cmdService;

        public async Task BuildCommandsAsync(IDiscordClient client)
        {
            this.client = (DiscordSocketClient)client;

            cmdService = new CommandService(new CommandServiceConfig()
            {
                LogLevel = Settings.LogSeverity,
                DefaultRunMode = RunMode.Async,
                CaseSensitiveCommands = false
            });          

            cmdService.CommandExecuted += CommandExecuted;

            await cmdService.AddModulesAsync(System.Reflection.Assembly.GetExecutingAssembly(), IoC.Kernel.GetRawKernel());

            BuiltCommands = true;
        }

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

                await context.Channel.SendMessageAsync($"**Error:** {result.ErrorReason}{(Settings.LogSeverity == LogSeverity.Debug ? $"\n**Exception type:** {result.Error}" : string.Empty)}");
            }

            await logger.LogAsync(msg);
        }

        public async Task HandleCommandsAsync(IMessage message)
        {
            if (!BuiltCommands)
                throw new Exception("You need to call the BuildCommands function before handling them.");

            if (!(message is SocketUserMessage msg))
                return;

            if (msg.Author.IsBot)
                return;

            if (message.Channel is IDMChannel && Settings.RedirectBotDMToOwner && !msg.Author.IsOwnerOfApp())           
                await (Utility.Utility.GetAppOwnerAsync().Result.SendMessageAsync($"**{message.Author}:** {message.Content}"));           

            var context = new SocketCommandContext(client, msg);

            int argPos = 0;

            if (!context.Message.HasStringPrefix(Settings.Prefix, ref argPos))
                return;

            await cmdService.ExecuteAsync(context, argPos, IoC.Kernel.GetRawKernel());
        }
    }
}
