using System;
using System.Collections.Generic;
using System.Text;

using Melodica.IoC;
using Melodica.Services.Services.Downloaders;

using Ninject.Modules;

namespace Melodica.Services.Downloaders
{
    public class DownloaderModule : NinjectModule
    {
        public override void Load()
        {
            Kernel.Bind<DownloaderProvider>().ToSelf();
        }
    }
}
