using Ninject.Modules;

namespace Melodica.Services.Lyrics;

public class LyricsModule : NinjectModule
{
    public override void Load()
    {
        Kernel.Bind<ILyricsProvider>().To<GeniusLyrics>();
    }
}
