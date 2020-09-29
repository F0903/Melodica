using Ninject.Modules;

namespace Melodica.Services.Lyrics
{
    public class LyricsModule : NinjectModule
    {
        public override void Load() => Kernel.Bind<LyricsProvider>().To<GeniusLyrics>();
    }
}
