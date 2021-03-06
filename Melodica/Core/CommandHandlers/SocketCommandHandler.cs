﻿using System;
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
        public SocketCommandHandler(IAsyncLogger logger)
        {
            this.logger = logger;
        }

        private readonly IAsyncLogger logger;

        private DiscordSocketClient? boundClient;

        private CommandService? cmdService;

        private async Task OnCommandExecuted(Optional<CommandInfo> info, ICommandContext context, IResult result)
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

        private async Task OnMessageReceived(SocketMessage message)
        {
            if (message is not SocketUserMessage msg)
                return;

            if (msg.Author.IsBot)
                return;

            var context = new SocketCommandContext(boundClient, msg);

            int argPos = 0;

            var guildSettings = await GuildSettings.GetSettingsAsync(context.Guild.Id);
            string? prefix = guildSettings.Prefix;
            if (!context.Message.HasStringPrefix(prefix, ref argPos))
                return;

            await cmdService!.ExecuteAsync(context, argPos, IoC.Kernel.GetRawKernel());
        }

        public Task HandleCommandsAsync(IDiscordClient client)
        {
            if (client is not DiscordSocketClient socketClient)
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