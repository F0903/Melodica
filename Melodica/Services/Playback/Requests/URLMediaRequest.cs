
using Melodica.Services.Media;
using Melodica.Utility.Extensions;

namespace Melodica.Services.Playback.Requests;

public class URLMediaRequest : IMediaRequest
{
    public URLMediaRequest(string mediaUrl)
    {
        info = new("") { Artist = "External", Title = mediaUrl, Url = mediaUrl };
        remote = mediaUrl;
    }

    static readonly HttpClient http = new();

    private readonly MediaInfo info;
    private readonly string remote;

    public Task<MediaCollection> GetMediaAsync()
    {
        TempMedia? media = new TempMedia(info, async (_) =>
        {
            Stream? data = await http.GetStreamAsync(remote);
            string? format = remote.AsSpan().ExtractFormatFromFileUrl();
            return new(data, format);
        });
        return Task.FromResult(new MediaCollection(media));
    }

    public Task<MediaInfo> GetInfoAsync()
    {
        return Task.FromResult(info);
    }
}
