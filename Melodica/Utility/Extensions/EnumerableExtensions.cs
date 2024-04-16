namespace Melodica.Utility.Extensions;
public static class EnumerableExtensions
{
    public static bool IsOverSize<T>(this IEnumerable<T> list, int exclusiveLimit)
    {
        var i = 0;
        foreach (var item in list)
        {
            ++i;
            if (i > exclusiveLimit)
                return true;
        }
        return false;
    }

    public static TimeSpan Sum<T>(this IEnumerable<T> input, Func<T, TimeSpan> selector)
    {
        TimeSpan sum = new();
        foreach (var item in input)
            sum += selector(item);
        return sum;
    }

    public static async Task<TimeSpan> SumAsync<T>(this IEnumerable<T> input, Func<T, Task<TimeSpan>> selector)
    {
        TimeSpan sum = new();
        foreach (var item in input)
            sum += await selector(item);
        return sum;
    }

    public static IEnumerable<To> Convert<From, To>(this IEnumerable<From> col, Func<From, To> body)
    {
        foreach (var elem in col)
            yield return body(elem);
    }
}
