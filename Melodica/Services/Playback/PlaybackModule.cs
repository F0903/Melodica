using Ninject.Modules;

namespace Melodica.Services.Playback
{
    public class PlaybackModule : NinjectModule
    {
        public override void Load()
        {
            Kernel.Bind<Jukebox>().ToSelf();
        }
    }
}
