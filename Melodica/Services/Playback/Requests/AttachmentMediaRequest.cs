using System;
using System.Net.Http;
using System.Threading.Tasks;

using Melodica.Services.Media;
using Melodica.Utility.Extensions;

namespace Melodica.Services.Playback.Requests
{
    public class AttachmentMediaRequest : IMediaRequest
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

        public Task<MediaInfo> GetInfoAsync()
        {
            return Task.FromResult(info ??= SetInfo());
        }

        public async Task<MediaCollection> GetMediaAsync()
        {
            var media = new TempMedia(await GetInfoAsync(), async (_) =>
            {
                var remote = attachment.Url;
                var data = await http.GetStreamAsync(remote);
                var format = remote.AsSpan().ExtractFormatFromFileUrl();
                return new DataPair(data, format);
            });
            return new MediaCollection(media);
        }
    }
}