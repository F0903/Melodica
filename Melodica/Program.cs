using System.Diagnostics;
using Melodica.Config;
using Melodica.Core;
using Serilog;


Melodica.Logging.LogManager.Init();
SocketBot bot = new();

await bot.ConnectAsync($"🎵", Discord.ActivityType.Listening, true);

void OnStop(object? sender, EventArgs args)
{
    bot.StopAsync().Wait();
    Log.CloseAndFlush();
}

AppDomain.CurrentDomain.ProcessExit += OnStop;

try
{
    Process.GetCurrentProcess()
        .PriorityClass = BotConfig.Settings.ProcessPriority;
}
catch (System.ComponentModel.Win32Exception ex)
{
    Log.Warning(ex, "Could not set process priority!");
}

await Task.Delay(-1);
