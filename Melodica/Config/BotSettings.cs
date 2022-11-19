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

public sealed class BotSettings
{ 
    public BotSettings(IConfigurationRoot config)
    {
        this.config = config;
        ReadAndSetValues();
    }

    readonly IConfigurationRoot config;

    void ReadAndSetValues()
    {  
        string? result;
        if ((result = config["cacheSizeMB"]) is null)
            throw new NullReferenceException("cacheSizeMB is not defined in user secrets!");
        CacheSizeMB = int.Parse(result.AsSpan());

        if ((result = config["processPriority"]) is null)
            throw new NullReferenceException("processPriority is not defined in user secrets!");
        ProcessPriority = Enum.Parse<ProcessPriorityClass>(result);

        if ((result = config["logLevel"]) is null)
            throw new NullReferenceException("logLevel is not defined in user secrets!");
        LogLevel = Enum.Parse<LogEventLevel>(result);

        if ((result = config["slashCommandDebugGuild"]) is null)
            throw new NullReferenceException("slashCommandDebugGuild is not defined in user secrets!");
        SlashCommandDebugGuild = ulong.Parse(result);
    }

    public void Reload()
    {
        ReadAndSetValues();
    } 

    public int CacheSizeMB { get; private set; }

    public ProcessPriorityClass ProcessPriority { get; private set; }

    public LogEventLevel LogLevel { get; private set; }

    public ulong SlashCommandDebugGuild { get; private set; }
}