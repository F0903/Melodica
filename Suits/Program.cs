﻿using Suits.Core;
using Suits.IoC;
using Suits.Core.Services.CommandHandlers;
using Suits.Core.Services;
using System;
using System.Threading.Tasks;
using System.Runtime.InteropServices;

namespace Suits
{
    public static class Program
    {
        private static readonly SocketBot currentBot = new SocketBot(BotSettings.GetOrSetSettings(() => new BotSettings() { LogSeverity = Discord.LogSeverity.Debug}), Kernel.Get<IAsyncLoggingService>(), Kernel.Get<SocketCommandHandler>());

        private static async Task Main()
        {
            await currentBot.ConnectAsync($"{GuildSettings.DefaultPrefix}play", Discord.ActivityType.Listening, true);
            AppDomain.CurrentDomain.ProcessExit += CurrentDomain_ProcessExit;

            await Task.Delay(-1);
        }

        private static void CurrentDomain_ProcessExit(object? sender, EventArgs e)
        {
            currentBot.StopAsync().Wait();
        }
    }
}