using System;
using System.Collections.Generic;
using System.Text;

using Melodica.Core.CommandHandlers;

using Ninject.Modules;

namespace Melodica.Core.CommandHandlers
{
    class CommandHandlerModule : NinjectModule
    {
        public override void Load()
        {
            Bind<IAsyncCommandHandler>().To<SocketCommandHandler>();
        }
    }
}
