namespace Melodica.Services.Audio;

internal interface IAsyncAudioProcessor : IDisposable
{
    public Task<ProcessorStreams> ProcessAsync();
}
