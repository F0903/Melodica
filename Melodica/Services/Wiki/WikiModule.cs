using System;
using System.Collections.Generic;
using System.Text;

using Ninject.Modules;

namespace Melodica.Services.Wiki
{
    public class WikiModule : NinjectModule
    {
        public override void Load()
        {
            Kernel.Bind<WikiProvider>().To<WikipediaWiki>();
        }
    }
}
