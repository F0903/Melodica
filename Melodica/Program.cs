﻿using Melodica.Core;
using Melodica.IoC;
using Melodica.Core.CommandHandlers;
using Melodica.Services.Logging;
using System;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Diagnostics;

namespace Melodica
{
    public static class Program
    {
        private static readonly SocketBot currentBot = new SocketBot(BotSettings.Get(true), Kernel.Get<IAsyncLoggingService>(), Kernel.Get<SocketCommandHandler>());

        private static async Task Main()
        {
            await currentBot.ConnectAsync($"{GuildSettings.DefaultPrefix}play", Discord.ActivityType.Listening, true);
            AppDomain.CurrentDomain.ProcessExit += CurrentDomain_ProcessExit;
            Process.GetCurrentProcess().PriorityClass = BotSettings.ProcessPriority;
            
            await Task.Delay(-1);
        }

        // Simply disconnect the bot automatically when the process is requested to close.
        private static void CurrentDomain_ProcessExit(object? sender, EventArgs e)
        {
            currentBot.StopAsync().Wait();
        }
    }
}