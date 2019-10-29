using System;
using System.Collections.Generic;
using System.Text;
using Ninject;
using Ninject.Modules;

namespace PokerBot.IoC.Modules
{
    public class DefaultModule : NinjectModule
    {
        public override void Load()
        {
            Bind<Core.IAsyncLogger>().To<Core.ColoredLogger>().InSingletonScope();
            Bind<Core.IAsyncCommandHandler>().To<Core.SocketCommandHandler>().InSingletonScope();
        }
    }
}
