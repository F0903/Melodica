using Melodica.Services.Media;

namespace Melodica.Services.Playback.Requests;

public interface IMediaRequest
{
    public Task<MediaInfo> GetInfoAsync();

    public Task<PlayableMedia> GetMediaAsync();
}
