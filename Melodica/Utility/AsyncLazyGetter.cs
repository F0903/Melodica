namespace Melodica.Utility;

public class AsyncLazyGetter<T> 
    where T : class 
{
    public AsyncLazyGetter(T value)
    {
        this.value = value;
        this.valueSource = null;
    }

    public AsyncLazyGetter(Func<Task<T>> valueSource)
    {
        this.valueSource = valueSource;
        this.value = null;
    }
    readonly T? value;

    readonly Func<Task<T>>? valueSource;

    public async Task<T> GetAsync()
    {
        return value ?? await valueSource!();
    }

    public static implicit operator AsyncLazyGetter<T>(Func<Task<T>> valueSource)
    {
        return new AsyncLazyGetter<T>(valueSource);
    }

    public static implicit operator AsyncLazyGetter<T>(T value)
    {
        return new AsyncLazyGetter<T>(value);
    }
}

public class AsyncParameterizedLazyGetter<T, A>
    where T : class
    where A : class
{
    public AsyncParameterizedLazyGetter(T value)
    {
        this.value = value;
        this.valueSource = null;
    }

    public AsyncParameterizedLazyGetter(Func<A, Task<T>> valueSource)
    {
        this.valueSource = valueSource;
        this.value = null;
    }
    readonly T? value;

    readonly Func<A, Task<T>>? valueSource;

    public async Task<T> GetAsync(A arg)
    {
        return value ?? await valueSource!(arg);
    }

    public static implicit operator AsyncParameterizedLazyGetter<T, A>(Func<A, Task<T>> valueSource)
    {
        return new AsyncParameterizedLazyGetter<T, A>(valueSource);
    }

    public static implicit operator AsyncParameterizedLazyGetter<T, A>(T value)
    {
        return new AsyncParameterizedLazyGetter<T, A>(value);
    }
}