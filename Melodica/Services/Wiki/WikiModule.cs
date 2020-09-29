﻿
using Ninject.Modules;

namespace Melodica.Services.Wiki
{
    public class WikiModule : NinjectModule
    {
        public override void Load() => Kernel.Bind<WikiProvider>().To<WikipediaWiki>();
    }
}
