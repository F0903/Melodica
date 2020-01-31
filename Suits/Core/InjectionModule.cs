namespace Suits
{
    public class InjectionModule : Ninject.Modules.NinjectModule
    {
        public override void Load()
        {
            Bind<Core.Services.Loggers.IAsyncLoggingService>().To<Core.Services.Loggers.ColoredLogger>();
            Bind<Core.Services.CommandHandlers.IAsyncCommandHandlerService>().To<Core.Services.CommandHandlers.SocketCommandHandler>().InSingletonScope();
        }
    }
}