using Melodica.Services.Caching;
using Melodica.Services.Media;
using Melodica.Utility.Extensions;

namespace Melodica.Services.Playback.Requests;

public sealed class URLMediaRequest(string mediaUrl) : IMediaRequest
{
    static readonly HttpClient http = new();

    private readonly MediaInfo info = new("") 
    { 
        Artist = "External", 
        Title = mediaUrl, 
        Url = mediaUrl 
    };
    private readonly string remote = mediaUrl;

    public async Task<PlayableMedia> GetMediaAsync()
    {
        var data = await http.GetStreamAsync(remote);
        var media = new PlayableMedia(data, info, null);
        return media;
    }

    public Task<MediaInfo> GetInfoAsync() => info.WrapTask();
}
