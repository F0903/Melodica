using System.Reflection;

using Discord;

using Serilog.Events;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;

namespace Melodica.Config;

public static class BotConfig
{
    static BotConfig()
    {
        var config = new ConfigurationBuilder()
            .AddUserSecrets(Assembly.GetExecutingAssembly(), false, true)
            .AddJsonFile("settings.json", false, true);
        Configuration = config.Build();

        ChangeToken.OnChange(Configuration.GetReloadToken, OnChange);
        var reloadTkn = Configuration.GetReloadToken();

        Secrets = new BotSecrets(Configuration);
        Settings = new BotSettings(Configuration);
    }

    public static BotSecrets Secrets { get; }

    public static BotSettings Settings { get; }

    public static IConfigurationRoot Configuration { get; }

    public static event Action? OnConfigChanged;

    static void OnChange()
    {
        Secrets.Reload();
        Settings.Reload();
        OnConfigChanged?.Invoke();
    }
}

public static class ConfigUtilityExtensions
{
    public static LogSeverity ToLogSeverity(this LogEventLevel logLevel)
    {
        return logLevel switch
        {
            LogEventLevel.Verbose => LogSeverity.Verbose,
            LogEventLevel.Debug => LogSeverity.Debug,
            LogEventLevel.Information => LogSeverity.Info,
            LogEventLevel.Warning => LogSeverity.Warning,
            LogEventLevel.Error => LogSeverity.Error,
            LogEventLevel.Fatal => LogSeverity.Critical,
            _ => throw new Exception("Unknown enum encountered in LogLevelToLogSeverity."),
        };
    }

    public static LogEventLevel ToLogEventLevel(this LogSeverity logLevel)
    {
        return logLevel switch
        {
            LogSeverity.Critical => LogEventLevel.Fatal,
            LogSeverity.Error => LogEventLevel.Error,
            LogSeverity.Warning => LogEventLevel.Warning,
            LogSeverity.Info => LogEventLevel.Information,
            LogSeverity.Verbose => LogEventLevel.Verbose,
            LogSeverity.Debug => LogEventLevel.Debug,
            _ => throw new Exception("Unknown enum encountered in LogLevelToLogSeverity."),
        };
    }
}
