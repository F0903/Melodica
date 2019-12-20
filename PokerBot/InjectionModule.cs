using System;
using System.Collections.Generic;
using System.Text;

namespace PokerBot
{
    public class InjectionModule : Ninject.Modules.NinjectModule
    {
        public override void Load()
        {
            Bind<Services.Loggers.IAsyncLoggingService>().To<Services.Loggers.ColoredLogger>();
            Bind<Services.CommandHandlers.IAsyncCommandHandlerService>().To<Services.CommandHandlers.SocketCommandHandler>().InSingletonScope();
        }
    }
}
