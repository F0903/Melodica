using Discord;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace PokerBot.Services
{
    public interface IAsyncJukeboxService
    {
        public Task PlayAsync(string songName);
        public Task StopAsync();
        public Task JoinChannelAsync(IVoiceChannel channel);
        public Task LeaveChannelAsync();
    }
}
