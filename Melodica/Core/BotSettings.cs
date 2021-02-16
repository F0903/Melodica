using System.Diagnostics;
using System.IO;

using Discord;

namespace Melodica.Core
{
    public static class BotSettings
    {
        private static string? cachedToken;
        public static string Token => cachedToken ??= File.ReadAllText("token.txt");

        public const string DefaultPrefix = "m.";

        public const int CacheSizeMB = 500;

        public const ProcessPriorityClass ProcessPriority = ProcessPriorityClass.High;

        public const LogSeverity LogLevel = LogSeverity.Debug;
    }
}