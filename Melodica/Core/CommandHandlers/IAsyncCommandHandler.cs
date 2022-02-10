using Discord;
using Discord.Commands;

namespace Melodica.Core.CommandHandlers;

public interface IAsyncCommandHandler
{
    Task OnMessageReceived(IMessage message);
}
