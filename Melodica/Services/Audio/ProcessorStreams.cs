namespace Melodica.Services.Audio;
internal record ProcessorStreams : IDisposable
{
    public Stream? Input { init; get; }
    public required Stream Output { init; get; }

    public void Dispose()
    {
        Input?.Dispose();
        Output?.Dispose();
    }
}