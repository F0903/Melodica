using System.Threading.Tasks;

namespace Melodica.Services.CommandHandlers
{
    public interface IAsyncCommandHandlerService
    {
        public Task HandleCommandsAsync(Discord.IMessage message);
    }
}