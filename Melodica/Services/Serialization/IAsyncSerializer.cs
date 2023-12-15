namespace Melodica.Services.Serialization;

public interface IAsyncSerializer
{
    public Task SerializeToFileAsync<T>(string path, T toSerialize);

    public Task<T> DeserializeFileAsync<T>(string path);
}
