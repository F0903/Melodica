using Ninject.Modules;

namespace Melodica.Services.Downloaders
{
    public class DownloaderModule : NinjectModule
    {
        public override void Load() => Kernel.Bind<DownloaderProvider>().ToSelf();
    }
}