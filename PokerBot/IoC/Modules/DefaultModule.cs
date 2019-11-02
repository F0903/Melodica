using System;
using System.Collections.Generic;
using System.Text;
using Ninject;
using Ninject.Modules;
using PokerBot.Core;
using PokerBot.Services;

namespace PokerBot.IoC.Modules
{
    public class DefaultModule : NinjectModule
    {
        public override void Load()
        {
            Bind<IAsyncLogger>().To<ColoredLogger>().InSingletonScope();
            Bind<IAsyncCommandHandler>().To<SocketCommandHandler>().InSingletonScope();
            Bind<IAsyncJukeboxService>().To<StandardJukeboxService>();
        }
    }
}
