using System.Text.Json;

namespace Melodica.Services.Serialization;

public sealed class BinarySerializer : IAsyncSerializer
{
    public Task SerializeToFileAsync<T>(string path, T toSerialize)
    {
        using var file = new FileStream(path, FileMode.OpenOrCreate, FileAccess.Write, FileShare.Write);
        return JsonSerializer.SerializeAsync(file, toSerialize);
    }

    public async Task<T> DeserializeFileAsync<T>(string path)
    {
        using var file = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        var obj = await JsonSerializer.DeserializeAsync<T>(file);
        return obj!;
    }
}
