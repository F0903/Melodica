using System.Reflection;

using Ninject;

namespace Melodica.IoC
{
    public static class Kernel
    {
        static Kernel() => kernel.Load(Assembly.GetExecutingAssembly());

        private static readonly IKernel kernel = new StandardKernel();

        public static IKernel GetRawKernel() => kernel;

        public static T Get<T>() => kernel.Get<T>();

        public static void RegisterInstance<T>(T instance) => kernel.Bind<T>().ToConstant(instance).InSingletonScope();
    }
}