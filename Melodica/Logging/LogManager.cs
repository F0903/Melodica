using Melodica.Config;
using Serilog;
using Serilog.Core;

namespace Melodica.Logging;
internal static class LogManager
{
    static LoggerConfiguration DefaultConfiguration => new LoggerConfiguration()
            .MinimumLevel.ControlledBy(logLevelSwitch)
            .WriteTo.Console();

    static string? lastLogPath = null;

    public static void Init()
    {
        SetupLogging();

        BotConfig.OnConfigChanged += OnConfigChanged;
    }

    static void SetupLogging()
    {
        var logPath = BotConfig.Settings.LogPath;
        if (logPath is not null)
        {
            Log.Logger = DefaultConfiguration.WriteTo.File(logPath).CreateLogger();
        }
        else
        {
            Log.Logger = DefaultConfiguration.CreateLogger();
        }
        lastLogPath = logPath;
    }

    static readonly LoggingLevelSwitch logLevelSwitch = new(BotConfig.Settings.LogLevel);

    static void OnConfigChanged()
    {
        var logLevel = logLevelSwitch.MinimumLevel = BotConfig.Settings.LogLevel;
        Log.Information($"Reconfigured Log MinimumLevel to {logLevel}", logLevel);

        var logPath = BotConfig.Settings.LogPath;
        if (logPath != lastLogPath) SetupLogging();
        lastLogPath = logPath;
    }

}
