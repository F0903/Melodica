namespace Melodica.Services.Serialization;

public interface IAsyncSerializer
{
    public static abstract Task SerializeToFileAsync<T>(string path, T toSerialize);

    public static abstract Task<T> DeserializeFileAsync<T>(string path);
}
