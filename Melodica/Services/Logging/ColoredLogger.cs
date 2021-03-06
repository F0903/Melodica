﻿using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Melodica.Services.Logging
{
    public class ColoredLogger : IAsyncLogger
    {
        static ColoredLogger()
        {
#if RELEASE
            Console.WriteLine("Release Mode");
#endif
        }

        public Task LogAsync(Discord.LogMessage msg)
        {
            [Conditional("DEBUG")]
            static void Log(Discord.LogMessage msg)
            {
                Console.ForegroundColor = msg.Severity switch
                {
                    Discord.LogSeverity.Critical => ConsoleColor.Red,
                    Discord.LogSeverity.Error => ConsoleColor.DarkRed,
                    Discord.LogSeverity.Warning => ConsoleColor.Yellow,
                    Discord.LogSeverity.Info => ConsoleColor.White,
                    Discord.LogSeverity.Verbose => ConsoleColor.Cyan,
                    Discord.LogSeverity.Debug => ConsoleColor.Blue,
                    _ => ConsoleColor.White
                };

                Console.WriteLine($"[{DateTime.Now} | {msg.Severity} | {msg.Source}] {msg.Message ?? msg.Exception.Message}");
            }

            Log(msg);
            return Task.CompletedTask;
        }
    }
}