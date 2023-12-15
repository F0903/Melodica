using Melodica.Dependencies;

using Microsoft.Extensions.DependencyInjection;

namespace Melodica.Services.Downloaders;

public sealed class DownloaderModule : DependencyModule
{
    public override IServiceCollection Load() => new ServiceCollection();
}
