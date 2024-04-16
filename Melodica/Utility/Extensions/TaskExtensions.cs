using System.Runtime.CompilerServices;

namespace Melodica.Utility.Extensions;
public static class TaskExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task<T> WrapTask<T>(this T value) => Task.FromResult(value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueTask<T> WrapValueTask<T>(this T value) => ValueTask.FromResult(value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static async Task<Y> Chain<T, Y>(this Task<T> me, Func<T, Task<Y>> chain)
    {
        return await chain(await me);
    }
}
