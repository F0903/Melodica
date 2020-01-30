using Discord;
using Discord.Commands;
using Discord.WebSocket;
using CasinoBot.Core.Services.Loggers;
using CasinoBot.Utility.Extensions;
using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace CasinoBot.Core.Services.CommandHandlers
{
    public class SocketCommandHandler : IAsyncCommandHandlerService
    {
        public SocketCommandHandler(IAsyncLoggingService logger)
        {
            this.logger = logger;

            Init();
        }

        public bool BuiltCommands { get; private set; } = false;

        private readonly IAsyncLoggingService logger;

        private CommandService cmdService;

        private DiscordSocketClient client;

        private void Init()
        {
            cmdService = new CommandService(new CommandServiceConfig()
            {
                LogLevel = CasinoBot.Settings.LogSeverity,
                DefaultRunMode = RunMode.Async,
                CaseSensitiveCommands = false
            });
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

                await context.Channel.SendMessageAsync($"**Error:** {result.ErrorReason}{(CasinoBot.Settings.LogSeverity == LogSeverity.Debug ? $"\n**Type:** {result.Error.Value}" : string.Empty)}");
            }

            await logger.LogAsync(msg);
        }

        public async Task BuildCommandsAsync(DiscordSocketClient client)
        {
            this.client = client;
            cmdService.CommandExecuted += CommandExecuted;

            // Add all projects or dlls ending in Module (can be removed just for this)
            var asms = AppDomain.CurrentDomain.GetAssemblies().Where(x => x.GetName().Name.Contains("Module")).ToArray();
            for (int i = -1; i < asms.Length; i++)
            {
                Assembly asm = null;
                if (i == -1)
                {
                    asm = Assembly.GetExecutingAssembly();
                }
                asm ??= asms[i];
                await cmdService.AddModulesAsync(asm, CasinoBot.IoC.Kernel.GetRawKernel());
            }

            BuiltCommands = true;
        }

        public async Task HandleCommandsAsync(IMessage message)
        {
            if (!BuiltCommands)
                throw new Exception("You need to call the BuildCommands function before handling them.");

            if (!(message is SocketUserMessage msg))
                return;

            if (msg.Author.IsBot)
                return;

            if (message.Channel is IDMChannel && CasinoBot.Settings.RedirectBotDMToOwner && !msg.Author.IsOwnerOfApp())
                await (CasinoBot.Utility.Utility.GetAppOwnerAsync().Result.SendMessageAsync($"**{message.Author}:** {message.Content}"));

            var context = new SocketCommandContext(client, msg);

            int argPos = 0;

            if (!context.Message.HasStringPrefix(CasinoBot.Settings.Prefix, ref argPos))
                return;

            await cmdService.ExecuteAsync(context, argPos, CasinoBot.IoC.Kernel.GetRawKernel());
        }
    }
}