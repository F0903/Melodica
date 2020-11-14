using Ninject.Modules;

namespace Melodica.Services.Logging
{
    public class LoggingModule : NinjectModule
    {
        public override void Load() => Bind<IAsyncLogger>().To<ColoredLogger>();
    }
}