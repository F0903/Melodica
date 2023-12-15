using System.Text.Json;

namespace Melodica.Services.Serialization;

public abstract class Serializer : IAsyncSerializer
{
    public static Task SerializeToFileAsync<T>(string path, T toSerialize)
    {
        using var file = new FileStream(path, FileMode.OpenOrCreate, FileAccess.Write, FileShare.Write);
        return JsonSerializer.SerializeAsync(file, toSerialize);
    }

    public static async Task<T> DeserializeFileAsync<T>(string path)
    {
        using var file = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        var obj = await JsonSerializer.DeserializeAsync<T>(file);
        return obj!;
    }
}
