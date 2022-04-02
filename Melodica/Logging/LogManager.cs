using Serilog;
using Serilog.Core;
using Serilog.Events;

using Melodica.Config;

namespace Melodica.Logging;
internal static class LogManager
{
    public static void Init()
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.ControlledBy(logLevelSwitch)
            .WriteTo.Console()
            .CreateLogger();

        BotConfig.OnConfigChanged += ReconfigureLogLevel;
    }

    static readonly LoggingLevelSwitch logLevelSwitch = new(BotConfig.Settings.LogLevel);

    static void ReconfigureLogLevel()
    {
        var logLevel = logLevelSwitch.MinimumLevel = BotConfig.Settings.LogLevel;
        Log.Information("Reconfigured Log MinimumLevel to {LogLevel}", logLevel);
    }
}
