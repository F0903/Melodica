namespace Melodica.Services.Audio;

public interface IAsyncAudioProcessor : IDisposable
{
    public void SetPause(bool value);
    public Task ProcessStreamAsync(Stream input, Stream output, Action? beforeInterruptionCallback, CancellationToken token, string? explicitDataFormat = null);
}
