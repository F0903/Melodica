using Melodica.Services.Media;
using Melodica.Utility;

namespace Melodica.Services.Playback.Requests;

public sealed class URLMediaRequest : IMediaRequest
{
    public URLMediaRequest(string mediaUrl)
    {
        info = new("") { Artist = "External", Title = mediaUrl, Url = mediaUrl };
        remote = mediaUrl;
    }

    static readonly HttpClient http = new();

    private readonly MediaInfo info;
    private readonly string remote;

    public async Task<PlayableMedia> GetMediaAsync()
    {
        var data = await http.GetStreamAsync(remote);
        var media = new PlayableMedia(data, info, null);
        return media;
    }

    public Task<MediaInfo> GetInfoAsync() => Task.FromResult(info);
}
