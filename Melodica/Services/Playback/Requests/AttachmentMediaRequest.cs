﻿
using Melodica.Services.Media;
using Melodica.Utility.Extensions;

namespace Melodica.Services.Playback.Requests;

public class AttachmentMediaRequest : IMediaRequest
{
    public AttachmentMediaRequest(Discord.Attachment[] attachments)
    {
        attachment = attachments[0];
    }

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

    public Task<MediaInfo> GetInfoAsync()
    {
        return Task.FromResult(info ??= SetInfo());
    }

    public async Task<MediaCollection> GetMediaAsync()
    {
        TempMedia? media = new TempMedia(await GetInfoAsync(), async (_) =>
        {
            string? remote = attachment.Url;
            Stream? data = await http.GetStreamAsync(remote);
            string? format = remote.AsSpan().ExtractFormatFromFileUrl();
            return new DataPair(data, format);
        });
        return new MediaCollection(media);
    }
}
