using System.Diagnostics;

using Discord;

namespace Melodica.Core
{
    public static class BotSettings
    {
        public const string Token = "NTcxNDAwNTc4MDY0ODQyNzUy.XMNMQQ.K2HzgSELITBxXoukQ7L502p2bVY";

        public const string DefaultPrefix = "m.";

        public const int CacheSizeMB = 500;

        public const ProcessPriorityClass ProcessPriority = ProcessPriorityClass.High;

        public const LogSeverity LogLevel = LogSeverity.Debug;
    }
}