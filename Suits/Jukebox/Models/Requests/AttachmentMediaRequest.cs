using System;
using System.Net;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Suits.Utility.Extensions;
using Suits.Jukebox.Services.Cache;

namespace Suits.Jukebox.Models.Requests
{
    class AttachmentMediaRequest : MediaRequest
    {
        public AttachmentMediaRequest(Discord.Attachment[] attachments)
        {
            attachment = attachments[0];
            for (int i = 1; i < attachments.Length; i++)
            {
                Requests.Add(new AttachmentMediaRequest(attachments[i]));
            }
        }

        private AttachmentMediaRequest(Discord.Attachment attachment)
        {
            this.attachment = attachment;
        }

        readonly Discord.Attachment? attachment;

        private Metadata? info;
        public override Metadata GetMediaInfo() => info ??= new Metadata() { Duration = TimeSpan.Zero, ID = null, Thumbnail = null, Title = attachment!.Filename, Format = Path.GetExtension(attachment.Filename).Replace(".", "") };

        public override Task<PlayableMedia> GetMediaAsync()
        {
            using var web = new WebClient();
            var data = web.DownloadData(attachment!.Url);
            
            return Task.FromResult((PlayableMedia)new TempMedia(GetMediaInfo(), data));
        }
    }
}
