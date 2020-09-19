using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading.Tasks;

using Melodica.Services.Models;

namespace Melodica.Services.Playback.Requests
{
    class AttachmentMediaRequest : MediaRequestBase
    {
        public AttachmentMediaRequest(Discord.Attachment[] attachments)
        {
            attachment = attachments[0];
        }

        readonly Discord.Attachment? attachment;

        private MediaMetadata? info;

        public override SubRequestInfo? SubRequestInfo { get; protected set; }
        public override List<MediaRequestBase>? SubRequests { get; set; }

        public override MediaMetadata GetInfo()
        {
            info ??= new MediaMetadata() { Duration = TimeSpan.Zero, ID = Path.ChangeExtension(attachment!.Filename, null), Thumbnail = null, Title = attachment!.Filename };
            info.DataInformation.Format = Path.GetExtension(attachment!.Filename).Replace(".", "");
            return info;
        }

        public override Task<PlayableMedia> GetMediaAsync()
        {
            using var web = new WebClient();
            var data = web.DownloadData(attachment!.Url);

            return Task.FromResult((PlayableMedia)new TempMedia(GetInfo(), new MemoryStream(data)));
        }
    }
}
