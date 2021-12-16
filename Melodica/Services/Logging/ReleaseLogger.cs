using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Discord;

namespace Melodica.Services.Logging
{
    public class ReleaseLogger : IAsyncLogger
    {
        static ReleaseLogger()
        {
            Console.WriteLine("Release Mode");
        }

        public Task LogAsync(LogMessage msg) { return Task.CompletedTask; }
    }
}
