using Melodica.Dependencies;

using Microsoft.Extensions.DependencyInjection;

namespace Melodica.Core.CommandHandlers;

public class CommandHandlerModule : DependencyModule
{
    public override IServiceCollection Load() =>
        new ServiceCollection()
        .AddSingleton<IAsyncCommandHandler, SocketHybridCommandHandler>();
}
