using System.Threading.Tasks;

namespace Melodica.Core.Services.CommandHandlers
{
    public interface IAsyncCommandHandlerService
    {
        public Task HandleCommandsAsync(Discord.IMessage message);
    }
}