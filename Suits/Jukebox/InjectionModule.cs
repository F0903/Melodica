using Suits.Jukebox.Services.Cache;
using Suits.Jukebox.Services.Downloaders;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;

namespace Suits.Jukebox
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