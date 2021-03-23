using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Ninject.Modules;

namespace Melodica.Services.Caching
{
    public class CachingModule : NinjectModule
    {
        public override void Load()
        {
            Kernel.Bind<IMediaCache>().To<MediaFileCache>();
        }
    }
}
