using System.Reflection;

using Microsoft.Extensions.DependencyInjection;

namespace Melodica.Dependencies;

public class DependencyModule
{
    public virtual IServiceCollection Load()
    {
        return new ServiceCollection();
    }
}

public static class Dependency
{
    static readonly IServiceProvider serviceProvider;

    static Dependency()
    {
        var asm = Assembly.GetExecutingAssembly();
        var modules = asm.ExportedTypes.Where(x => x.IsSubclassOf(typeof(DependencyModule)));
        IServiceCollection services = new ServiceCollection();
        foreach (var modType in modules)
        {
            var modConstructor = modType.GetConstructor(Type.EmptyTypes);
            if (modConstructor is null)
                continue;
            var modObject = (DependencyModule)modConstructor.Invoke(null);
            var modServices = modObject.Load();
            foreach (var service in modServices)
            {
                services.Add(service);
            }
        }
        serviceProvider = services.BuildServiceProvider();
    }

    public static IServiceProvider GetServiceProvider() => serviceProvider;

    public static T Get<T>() where T : notnull => serviceProvider.GetRequiredService<T>();
}

