using Ninject;
using System;

namespace CasinoBot.IoC
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

        public static Ninject.Syntax.IBindingToSyntax<T> Bind<T>() => kernel.Bind<T>();

        public static Ninject.Syntax.IBindingToSyntax<object> Bind(params Type[] types) => kernel.Bind(types);
    }
}