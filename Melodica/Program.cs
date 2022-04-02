using System.Diagnostics;

using Melodica.Core;
using Melodica.Core.CommandHandlers;
using Melodica.Config;

using Serilog;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.Console(restrictedToMinimumLevel: BotConfig.Settings.LogLevel)
    .CreateLogger();

var bot = new SocketBot<IAsyncCommandHandler>();

await bot.ConnectAsync($"{BotConfig.Settings.DefaultPrefix}play | {BotConfig.Settings.DefaultPrefix}help", Discord.ActivityType.Listening, true);

void OnStop(object? sender, EventArgs args)
{
    bot.StopAsync().Wait();
    Log.CloseAndFlush();
}

AppDomain.CurrentDomain.ProcessExit += OnStop;

Process.GetCurrentProcess()
    .PriorityClass = BotConfig.Settings.ProcessPriority;

await Task.Delay(-1);
