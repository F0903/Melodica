namespace Melodica.Core.CommandHandlers;

public interface IAsyncCommandHandler
{
    Task InitializeCommandsAsync();
}
