using System.Diagnostics;

using Melodica.Core;
using Melodica.Core.CommandHandlers;
using Melodica.IoC;
using Melodica.Services.Logging;

var bot = new SocketBot<IAsyncCommandHandler>(Kernel.Get<IAsyncLogger>());

await bot.ConnectAsync($"{BotSettings.TextCommandPrefix}play | {BotSettings.TextCommandPrefix}help", Discord.ActivityType.Listening, true);

AppDomain.CurrentDomain.ProcessExit += (sender, args) => bot.StopAsync().Wait();
AppDomain.CurrentDomain.UnhandledException += (sender, args) => File.WriteAllText("./error.txt", (args.ExceptionObject as Exception)?.ToString() ?? "Exception was null.");

Process.GetCurrentProcess().PriorityClass = BotSettings.ProcessPriority;

await Task.Delay(-1);
