using Melodica.Dependencies;

using Microsoft.Extensions.DependencyInjection;

namespace Melodica.Services.Downloaders;

public class DownloaderModule : DependencyModule
{
    public override IServiceCollection Load()
    {
        return new ServiceCollection();
    }
}
