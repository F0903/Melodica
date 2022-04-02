using System.Diagnostics;
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

        var reloadTkn = Configuration.GetReloadToken();

        Secrets = new BotSecrets(Configuration);
        Secrets.ConfigureReload(reloadTkn);

        Settings = new BotSettings(Configuration);
        Settings.ConfigureReload(reloadTkn);
    }

    public static BotSecrets Secrets { get; }

    public static BotSettings Settings { get; }

    public static IConfigurationRoot Configuration { get; }
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
}
