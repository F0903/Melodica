using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Ninject;

namespace PokerBot.IoC
{
    public static class Kernel
    {
        static Kernel()
        {
            kernel.Load(AppDomain.CurrentDomain.GetAssemblies());
        }

        private readonly static IKernel kernel = new StandardKernel();

        public static IKernel GetRawKernel() => kernel;

        public static T Get<T>() => kernel.Get<T>();

        public static void RegisterInstance<T>(T instance)
        {
            kernel.Bind<T>().ToConstant(instance).InSingletonScope();
        }

    }
}
