
using Melodica.Dependencies;

using Microsoft.Extensions.DependencyInjection;

namespace Melodica.Services.Logging;

public class LoggingModule : DependencyModule
{
    public override IServiceCollection Load()
    {
        return new ServiceCollection()
#if DEBUG
            .AddSingleton<IAsyncLogger, ColoredLogger>();
#else
            .AddSingleton<IAsyncLogger, ReleaseLogger>();
#endif
    }
}
