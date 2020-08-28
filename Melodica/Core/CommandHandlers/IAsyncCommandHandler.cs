using System.Threading.Tasks;

namespace Melodica.Core.CommandHandlers
{
    public interface IAsyncCommandHandler
    {
        public Task HandleCommandsAsync(Discord.IMessage message);
    }
}