
using Ninject.Modules;

namespace Melodica.Services.Settings
{
    public class SettingsModule : NinjectModule
    {
        public override void Load() => Kernel.Bind<GuildSettingsProvider>().ToSelf();
    }
}
