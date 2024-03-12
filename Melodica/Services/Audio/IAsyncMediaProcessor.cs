using Melodica.Services.Caching;
using Melodica.Services.Media;

namespace Melodica.Services.Audio;

public interface IAsyncMediaProcessor : IDisposable
{
    public void SetPause(bool value);
    public Task ProcessMediaAsync(PlayableMediaStream media, Stream output, Action? beforeInterruptionCallback, CancellationToken token);
}
