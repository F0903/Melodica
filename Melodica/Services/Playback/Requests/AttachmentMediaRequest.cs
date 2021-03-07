using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading.Tasks;

using Melodica.Services.Media;

namespace Melodica.Services.Playback.Requests
{
    internal class AttachmentMediaRequest : IMediaRequest
    {
        public AttachmentMediaRequest(Discord.Attachment[] attachments) => attachment = attachments[0];

        private readonly Discord.Attachment? attachment;

        private MediaInfo? info;

        public Task<MediaInfo> GetInfoAsync()
        {
            info ??= new MediaInfo() { Duration = TimeSpan.Zero, Id = Path.ChangeExtension(attachment!.Filename, null), ImageUrl = null, Title = attachment!.Filename };
            info.DataInformation.Format = Path.GetExtension(attachment!.Filename).Replace(".", "");
            return Task.FromResult(info);
        }

        public async Task<MediaCollection> GetMediaAsync()
        {
            using var web = new WebClient();
            byte[]? data = web.DownloadData(attachment!.Url);
            var info = await GetInfoAsync();
            return new MediaCollection(new TempMedia(info, (_) => Task.FromResult(((Stream)new MemoryStream(data), ""))));
        }
    }
}