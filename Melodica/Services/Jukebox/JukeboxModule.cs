﻿using System;
using System.Collections.Generic;
using System.Text;

using Ninject.Modules;

namespace Melodica.Services.Jukebox
{
    public class JukeboxModule : NinjectModule
    {
        public override void Load()
        {
            Kernel.Bind<Jukebox>().ToSelf();
        }
    }
}
