using System.Diagnostics;

using Discord;

namespace Melodica.Core
{
    public static class BotSettings
    {
        public const string Token = "NTcxNDAwNTc4MDY0ODQyNzUy.XMNMQQ.8K9ovB1sbkExbYIK2wCI6OxzXSw";

        public const string DefaultPrefix = "m.";

        public const int CacheSizeMB = 500;

        public const ProcessPriorityClass ProcessPriority = ProcessPriorityClass.High;

        // Avoid setting this to debug, as it will spam a debug message when playing audio, slowing the bot down.
        public const LogSeverity LogLevel = LogSeverity.Verbose;
    }
}
