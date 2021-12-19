
using Ninject.Modules;

namespace Melodica.Services.Caching;

public class CachingModule : NinjectModule
{
    public override void Load()
    {
        Kernel.Bind<IMediaCache>().To<MediaFileCache>();
    }
}
