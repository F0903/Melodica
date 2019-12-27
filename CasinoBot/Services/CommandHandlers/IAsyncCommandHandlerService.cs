using System.Threading.Tasks;

namespace CasinoBot.Services.CommandHandlers
{
    public interface IAsyncCommandHandlerService
    {
        public Task HandleCommandsAsync(Discord.IMessage message);

        public Task BuildCommandsAsync(Discord.WebSocket.DiscordSocketClient client);
    }
}