using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace PokerBot.Core
{
    public interface IAsyncBot
    {
        public Task ConnectAsync(bool startOnConnect = false);
        public Task StartAsync();
        public Task StopAsync();
    }
}
