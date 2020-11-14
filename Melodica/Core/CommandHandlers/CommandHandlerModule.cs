using Ninject.Modules;

namespace Melodica.Core.CommandHandlers
{
    internal class CommandHandlerModule : NinjectModule
    {
        public override void Load() => Bind<IAsyncCommandHandler>().To<SocketCommandHandler>();
    }
}