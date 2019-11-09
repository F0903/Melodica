using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace PokerBot.Services
{
    public interface IAsyncCommandHandlerService
    {
        public Task HandleCommandsAsync(Discord.IMessage message);

        public Task BuildCommandsAsync(Discord.IDiscordClient client);
    }
}
