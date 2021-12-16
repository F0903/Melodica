using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

using Melodica.Core;
using Melodica.Core.CommandHandlers;
using Melodica.IoC;
using Melodica.Services.Logging;

SocketBot currentBot = new SocketBot(Kernel.Get<IAsyncLogger>(), Kernel.Get<SocketCommandHandler>());

await currentBot.ConnectAsync($"{BotSettings.DefaultPrefix}play | {BotSettings.DefaultPrefix}help", Discord.ActivityType.Listening, true);

AppDomain.CurrentDomain.ProcessExit += (sender, args) => currentBot.StopAsync().Wait();
AppDomain.CurrentDomain.UnhandledException += (sender, args) => File.WriteAllText("./error.txt", (args.ExceptionObject as Exception)?.ToString() ?? "Exception was null.");

Process.GetCurrentProcess().PriorityClass = BotSettings.ProcessPriority;

await Task.Delay(-1);
