using Ninject;
using System;
using System.Reflection;

namespace Melodica.IoC
{
    public static class Kernel
    {
        static Kernel()
        {
            kernel.Load(Assembly.GetExecutingAssembly());
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