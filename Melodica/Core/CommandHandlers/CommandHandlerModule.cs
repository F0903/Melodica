using Melodica.Dependencies;

using Microsoft.Extensions.DependencyInjection;

namespace Melodica.Core.CommandHandlers;

public sealed class CommandHandlerModule : DependencyModule
{
    public override IServiceCollection Load() =>
        new ServiceCollection()
        .AddSingleton<IAsyncCommandHandler, SocketCommandHandler>();
}
