using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace PokerBot.Services
{
    public interface IAsyncLogger
    {
        public Task LogAsync(Discord.LogMessage msg);
    }
}
