using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

using Discord;

namespace Melodica.Core
{
    public static class BotSettings
    {
        public const string Token = "NTcxNDAwNTc4MDY0ODQyNzUy.XMNMQQ.8K9ovB1sbkExbYIK2wCI6OxzXSw";

        public const string DefaultPrefix = "m.";

        public const int CacheSizeMB = 500;

        public const ProcessPriorityClass ProcessPriority = ProcessPriorityClass.High;

        public const LogSeverity LogLevel = LogSeverity.Debug;
    }
}
