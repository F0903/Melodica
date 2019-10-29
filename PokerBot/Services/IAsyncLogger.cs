using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace PokerBot.Core
{
    public interface IAsyncLogger
    {
        public Task LogAsync(Discord.LogMessage msg);
    }
}
