namespace Suits.Core
{
    public partial class InjectionModule : Ninject.Modules.NinjectModule
    {
        public override void Load()
        {
            Bind<Services.IAsyncLoggingService>().To<Services.ColoredLogger>();
            Bind<Services.CommandHandlers.IAsyncCommandHandlerService>().To<Services.CommandHandlers.SocketCommandHandler>();//.InSingletonScope();
        }
    }
}