using Ninject.Modules;

namespace Melodica.Services.Playback
{
    public class JukeboxModule : NinjectModule
    {
        public override void Load()
        {
            Kernel.Bind<Jukebox>().ToSelf();
        }
    }
}
