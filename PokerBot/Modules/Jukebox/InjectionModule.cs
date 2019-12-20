using PokerBot.Modules.Jukebox.Services.Cache;
using PokerBot.Modules.Jukebox.Services.Downloaders;
using System;
using System.Collections.Generic;
using System.Text;

namespace PokerBot.Modules.Jukebox
{
    public class InjectionModule : Ninject.Modules.NinjectModule
    {
        public override void Load()
        {
            Bind<IAsyncMediaCache>().To<AsyncMediaFileCache>();

            Bind<IAsyncDownloadService>().To<AsyncYoutubeDownloader>();

            Bind<JukeboxService>().ToSelf().InSingletonScope(); // This MUST be made in singleton scope, else will not work.
        }
    }
}
