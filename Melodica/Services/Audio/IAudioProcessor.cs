namespace Melodica.Services.Audio;

public interface IAsyncAudioProcessor : IDisposable
{
    public ValueTask<int> ProcessStreamAsync(Memory<byte> memory);
}
