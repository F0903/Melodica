using Melodica.Config;
using Serilog;
using Serilog.Core;

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
        Log.Information($"Reconfigured Log MinimumLevel to {logLevel}", logLevel);
    }
}
