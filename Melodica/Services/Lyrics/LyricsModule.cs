﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Ninject.Modules;

namespace Melodica.Services.Lyrics
{
    public class LyricsModule : NinjectModule
    {
        public override void Load()
        {
            Kernel.Bind<LyricsProvider>().To<GeniusLyrics>();
        }
    }
}