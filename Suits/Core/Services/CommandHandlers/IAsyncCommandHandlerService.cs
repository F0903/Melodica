using System.Threading.Tasks;

namespace Suits.Core.Services.CommandHandlers
{
    public interface IAsyncCommandHandlerService
    {
        public Task HandleCommandsAsync(Discord.IMessage message);
    }
}