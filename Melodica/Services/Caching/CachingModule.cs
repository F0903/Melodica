
using Melodica.Dependencies;

using Microsoft.Extensions.DependencyInjection;

namespace Melodica.Services.Caching;

public class CachingModule : DependencyModule
{
    public override IServiceCollection Load() =>
        new ServiceCollection()
        .AddSingleton<IMediaCache, MediaFileCache>();
}
