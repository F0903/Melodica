using System;
using System.Collections.Generic;
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
    }
}
