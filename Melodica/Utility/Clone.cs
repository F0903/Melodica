using System.Reflection;
using System.Runtime.CompilerServices;

namespace Melodica.Utility;
public static class Clone
{
    static readonly MethodInfo memberwiseCloneMethod = typeof(object).GetMethod("MemberwiseClone", BindingFlags.NonPublic | BindingFlags.Instance)!;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T ShallowClone<T>(this T obj)
    {
        return (T)(memberwiseCloneMethod.Invoke(obj, null) ?? throw new NullReferenceException("Could not member-wise clone object!"));
    }
}
