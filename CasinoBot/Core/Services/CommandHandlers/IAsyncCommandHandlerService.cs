using System.Threading.Tasks;

namespace CasinoBot.Core.Services.CommandHandlers
{
    public interface IAsyncCommandHandlerService
    {
        public Task HandleCommandsAsync(Discord.IMessage message);

        public Task BuildCommandsAsync(Discord.WebSocket.DiscordSocketClient client);
    }
}