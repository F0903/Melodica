using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading.Tasks;

using Melodica.Services.Models;

namespace Melodica.Services.Playback.Requests
{
    internal class AttachmentMediaRequest : MediaRequest
    {
        public AttachmentMediaRequest(Discord.Attachment[] attachments) => attachment = attachments[0];

        private readonly Discord.Attachment? attachment;

        private MediaMetadata? info;

        public override MediaMetadata? ParentRequestInfo { get; protected set; }
        public override List<MediaRequest>? SubRequests { get; set; }

        public override MediaMetadata GetInfo()
        {
            info ??= new MediaMetadata() { Duration = TimeSpan.Zero, Id = Path.ChangeExtension(attachment!.Filename, null), Thumbnail = null, Title = attachment!.Filename };
            info.DataInformation.Format = Path.GetExtension(attachment!.Filename).Replace(".", "");
            return info;
        }

        public override Task<PlayableMedia> GetMediaAsync()
        {
            using var web = new WebClient();
            byte[]? data = web.DownloadData(attachment!.Url);

            return Task.FromResult((PlayableMedia)new TempMedia(GetInfo(), new MemoryStream(data)));
        }
    }
}
