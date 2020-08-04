using Melodica.Services.Logging;

namespace Melodica.Core
{
    public partial class InjectionModule : Ninject.Modules.NinjectModule
    {
        public override void Load()
        {
            Bind<IAsyncLoggingService>().To<ColoredLogger>();
            Bind<Services.CommandHandlers.IAsyncCommandHandlerService>().To<Services.CommandHandlers.SocketCommandHandler>();//.InSingletonScope();
        }
    }
}