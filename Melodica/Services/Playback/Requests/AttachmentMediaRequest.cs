using System.Text;
using Melodica.Services.Caching;
using Melodica.Services.Media;
using Melodica.Utility;

namespace Melodica.Services.Playback.Requests;

public sealed class AttachmentMediaRequest(Discord.Attachment[] attachments) : IMediaRequest
{
    static readonly HttpClient http = new();

    private readonly Discord.Attachment attachment = attachments[0];

    private MediaInfo? info;

    MediaInfo SetInfo()
    {
        return new(attachment.Id.ToString())
        {
            Artist = "Attachment",
            Title = attachment.Filename,
            Url = attachment.Url
        };
    }

    public Task<MediaInfo> GetInfoAsync() => (info ??= SetInfo()).WrapTask();

    public async Task<PlayableMediaStream> GetMediaAsync()
    {
        var remote = attachment.Url;
        var data = await http.GetStreamAsync(remote);
        var media = new PlayableMediaStream(data, await GetInfoAsync(), null, null);
        return media;
    }
}
