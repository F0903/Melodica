using CasinoBot.Modules.Jukebox.Services.Cache;
using CasinoBot.Modules.Jukebox.Services.Downloaders;

namespace CasinoBot.Modules.Jukebox
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