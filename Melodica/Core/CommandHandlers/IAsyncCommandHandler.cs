using System.Threading.Tasks;

using Discord;

namespace Melodica.Core.CommandHandlers
{
    public interface IAsyncCommandHandler
    {
        public Task HandleCommandsAsync(IDiscordClient clientToHandle);
    }
}