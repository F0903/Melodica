using System;
using System.Collections.Generic;
using System.Text;

namespace PokerBot.Modules.Jukebox
{
    public class InjectionModule : Ninject.Modules.NinjectModule
    {
        public override void Load()
        {
            Bind<JukeboxService>().ToSelf().InSingletonScope();
        }
    }
}
