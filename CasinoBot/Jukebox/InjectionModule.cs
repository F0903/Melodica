using CasinoBot.Jukebox.Services.Cache;
using CasinoBot.Jukebox.Services.Downloaders;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;

namespace CasinoBot.Jukebox
{
    public class InjectionModule : Ninject.Modules.NinjectModule
    {
        public override void Load()
        {
            Bind<IFormatter>().To<BinaryFormatter>().InSingletonScope();

            Bind<IAsyncDownloadService>().To<AsyncYoutubeDownloader>();

            Bind<JukeboxService>().ToSelf().InSingletonScope(); // This MUST be made in singleton scope, else will not work.
        }
    }
}