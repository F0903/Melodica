using System.Diagnostics;

using Discord;

namespace Melodica.Core;

public static class BotSettings
{
    public const string TextCommandPrefix = "m.";

    public const int CacheSizeMB = 500;

    public const ProcessPriorityClass ProcessPriority = ProcessPriorityClass.High;

    public const LogSeverity LogLevel = LogSeverity.Debug;

    public const ulong SlashCommandDebugGuild = 153896159834800129;
}
