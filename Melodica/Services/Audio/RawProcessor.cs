namespace Melodica.Services.Audio;

internal class RawProcessor : IAsyncAudioProcessor
{
    internal RawProcessor(string file) => this.file = file;

    readonly string file;

    public Task<ProcessorStreams> ProcessAsync()
    {
        // Don't dispose, we have to keep it open for the reader later.
        var fileStream = File.OpenRead(file);
        var streams = new ProcessorStreams { Output = fileStream };
        return Task.FromResult(streams);
    }

    public void Dispose() { }
}
