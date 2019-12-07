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
            Bind<IAsyncLoggingService>().To<ColoredLogger>().InSingletonScope();

            Bind<IAsyncCommandHandlerService>().To<SocketCommandHandler>().InSingletonScope();

            Bind<IAsyncDownloadService>().To<AsyncYoutubeDownloader>();

            Bind<AsyncFileCache>().ToSelf().InSingletonScope();
        }
    }
}
