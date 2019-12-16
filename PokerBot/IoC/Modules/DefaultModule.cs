using System;
using System.Collections.Generic;
using System.Text;
using Ninject;
using Ninject.Modules;
using PokerBot.Core;
using PokerBot.Services;
using PokerBot.Services.Cache;
using PokerBot.Services.CommandHandlers;
using PokerBot.Services.Downloaders;
using PokerBot.Services.Loggers;

namespace PokerBot.IoC.Modules
{
    public class DefaultModule : NinjectModule
    {
        public override void Load()
        {
            Bind<IAsyncLoggingService>().To<ColoredLogger>().InSingletonScope();

            Bind<IAsyncCommandHandlerService>().To<SocketCommandHandler>().InSingletonScope();

            Bind<IAsyncDownloadService>().To<AsyncYoutubeDownloader>();

            Bind<AsyncMediaFileCache>().ToSelf().InSingletonScope();
        }
    }
}
