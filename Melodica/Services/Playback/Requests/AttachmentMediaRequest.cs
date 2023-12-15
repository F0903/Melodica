
using Melodica.Services.Media;
using Melodica.Utility;

namespace Melodica.Services.Playback.Requests;

public sealed class AttachmentMediaRequest : IMediaRequest
{
    public AttachmentMediaRequest(Discord.Attachment[] attachments) => attachment = attachments[0];

    static readonly HttpClient http = new();

    private readonly Discord.Attachment attachment;

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

    public Task<MediaInfo> GetInfoAsync() => Task.FromResult(info ??= SetInfo());

    public async Task<MediaCollection> GetMediaAsync()
    {
        TempMedia? media = new(await GetInfoAsync(), async (_) =>
        {
            var remote = attachment.Url;
            var data = await http.GetStreamAsync(remote);
            var format = remote.AsSpan().ExtractFormatFromFileUrl();
            return new DataPair(data, format);
        });
        return new MediaCollection(media);
    }
}
