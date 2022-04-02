using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;

using Serilog.Events;

namespace Melodica.Config;

public class BotSettings
{
    //REASON: Redundant warning.
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    public BotSettings(IConfigurationRoot config)
    {
        this.config = config;
        ReadAndSetValues();
    }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

    readonly IConfigurationRoot config;

    void ReadAndSetValues()
    {
        if ((DefaultPrefix = config["defaultPrefix"]) is null)
            throw new NullReferenceException("defaultPrefix is not defined in user secrets!");

        string result;
        if ((result = config["cacheSizeMB"]) is null)
            throw new NullReferenceException("cacheSizeMB is not defined in user secrets!");
        CacheSizeMB = int.Parse(result.AsSpan());

        if ((result = config["processPriority"]) is null)
            throw new NullReferenceException("processPriority is not defined in user secrets!");
        ProcessPriority = Enum.Parse<ProcessPriorityClass>(result);

        if ((result = config["logLevel"]) is null)
            throw new NullReferenceException("logLevel is not defined in user secrets!");
        LogLevel = Enum.Parse<LogEventLevel>(result);
    }

    public void ConfigureReload(IChangeToken token)
    {
        token.RegisterChangeCallback(Reload, null);
    }

    void Reload(object? state)
    {
        ReadAndSetValues();
    }

    public string DefaultPrefix { get; private set; }

    public int CacheSizeMB { get; private set; }

    public ProcessPriorityClass ProcessPriority { get; private set; }

    public LogEventLevel LogLevel { get; private set; }
}