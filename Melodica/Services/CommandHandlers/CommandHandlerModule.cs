using System;
using System.Collections.Generic;
using System.Text;

using Ninject.Modules;

namespace Melodica.Services.CommandHandlers
{
    class CommandHandlerModule : NinjectModule
    {
        public override void Load()
        {
            Bind<IAsyncCommandHandlerService>().To<SocketCommandHandler>();
        }
    }
}
