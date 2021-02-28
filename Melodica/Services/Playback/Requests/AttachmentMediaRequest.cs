using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading.Tasks;

using Melodica.Services.Media;

namespace Melodica.Services.Playback.Requests
{
    internal class AttachmentMediaRequest : MediaRequest
    {
        public AttachmentMediaRequest(Discord.Attachment[] attachments) => attachment = attachments[0];

        private readonly Discord.Attachment? attachment;

        private MediaInfo? info;

        public override MediaInfo? ParentRequestInfo { get; protected set; }
        public override List<MediaRequest>? SubRequests { get; set; }

        public override MediaInfo GetInfo()
        {
            info ??= new MediaInfo() { Duration = TimeSpan.Zero, Id = Path.ChangeExtension(attachment!.Filename, null), ImageUrl = null, Title = attachment!.Filename };
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