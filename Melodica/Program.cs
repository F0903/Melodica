﻿using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

using Melodica.Core;
using Melodica.Core.CommandHandlers;
using Melodica.IoC;
using Melodica.Services.Logging;

namespace Melodica
{
    public static class Program
    {
        private static readonly SocketBot currentBot = new SocketBot(Kernel.Get<IAsyncLogger>(), Kernel.Get<SocketCommandHandler>());

        private static async Task Main()
        {
            await currentBot.ConnectAsync($"{BotSettings.DefaultPrefix}play | {BotSettings.DefaultPrefix}help", Discord.ActivityType.Listening, true);

            AppDomain.CurrentDomain.ProcessExit += CurrentDomain_ProcessExit;
            AppDomain.CurrentDomain.UnhandledException += (sender, args) => File.WriteAllText("./error.txt", (args.ExceptionObject as Exception)?.ToString() ?? "Exception was null.");

            Process.GetCurrentProcess().PriorityClass = BotSettings.ProcessPriority;

            await Task.Delay(-1);
        }

        // Simply disconnect the bot automatically when the process is requested to close.
        private static void CurrentDomain_ProcessExit(object? sender, EventArgs e) => currentBot.StopAsync().Wait();
    }
}