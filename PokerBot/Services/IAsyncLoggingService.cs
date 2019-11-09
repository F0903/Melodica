using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace PokerBot.Services
{
    public interface IAsyncLoggingService
    {
        public Task LogAsync(Discord.LogMessage msg);
    }
}
