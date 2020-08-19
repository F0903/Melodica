using System;
using System.Collections.Generic;
using System.Text;

using Ninject.Modules;

namespace Melodica.Services.Logging
{
    public class LoggingModule : NinjectModule
    {
        public override void Load()
        {
            Bind<IAsyncLoggingService>().To<ColoredLogger>();
        }
    }
}
