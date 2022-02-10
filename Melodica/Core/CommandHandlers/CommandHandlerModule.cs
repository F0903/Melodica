using Ninject.Modules;

namespace Melodica.Core.CommandHandlers;

public class CommandHandlerModule : NinjectModule
{
    public override void Load()
    {
        Bind<IAsyncCommandHandler>().To<SocketHybridCommandHandler>();
    }
}
