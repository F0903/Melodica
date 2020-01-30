using CasinoBot.Jukebox.Services.Cache;
using CasinoBot.Jukebox.Services.Downloaders;

namespace CasinoBot.Jukebox
{
    public class InjectionModule : Ninject.Modules.NinjectModule
    {
        public override void Load()
        {
            Bind<IAsyncDownloadService>().To<AsyncYoutubeDownloader>();

            Bind<JukeboxService>().ToSelf().InSingletonScope(); // This MUST be made in singleton scope, else will not work.
        }
    }
}