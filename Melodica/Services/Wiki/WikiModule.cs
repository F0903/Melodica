using Melodica.Dependencies;

using Microsoft.Extensions.DependencyInjection;

namespace Melodica.Services.Wiki;

public sealed class WikiModule : DependencyModule
{
    public override IServiceCollection Load() =>
        new ServiceCollection()
        .AddTransient<IWikiProvider, WikipediaWiki>();
}
