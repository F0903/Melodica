
using Ninject.Modules;

namespace Melodica.Services.Logging;

public class LoggingModule : NinjectModule
{
    public override void Load()
    {
#if DEBUG
        Bind<IAsyncLogger>().To<ColoredLogger>();
#else
        Bind<IAsyncLogger>().To<ReleaseLogger>();
#endif
    }
}
