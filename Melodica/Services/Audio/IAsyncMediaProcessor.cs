using Melodica.Services.Caching;
using Melodica.Services.Media;

namespace Melodica.Services.Audio;

public interface IAsyncMediaProcessor : IDisposable
{
    public void SetPause(bool value);
    public Task ProcessMediaAsync(PlayableMedia media, Stream output, Action? onHalt, Action? onResume, int bufferSize = 3840, CancellationToken cancellationToken = default);
}
