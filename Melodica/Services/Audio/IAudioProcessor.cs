namespace Melodica.Services.Audio;
internal interface IAsyncAudioProcessor : IDisposable
{
    public ValueTask<Stream> ProcessAsync();
}
