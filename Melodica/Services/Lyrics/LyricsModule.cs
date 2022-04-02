using Melodica.Dependencies;

using Microsoft.Extensions.DependencyInjection;

namespace Melodica.Services.Lyrics;

public class LyricsModule : DependencyModule
{
    public override IServiceCollection Load() =>
        new ServiceCollection()
        .AddTransient<ILyricsProvider, GeniusLyrics>();
}
