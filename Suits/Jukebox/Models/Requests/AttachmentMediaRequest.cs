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
            Requests.Add(this);
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

        public override Task<PlayableMedia> GetMediaAsync()
        {
            using var web = new WebClient();
            var data = web.DownloadData(attachment.Url);
            var name = attachment.Filename;
            var format = Path.GetExtension(name).Replace(".", "");
            return Task.FromResult((PlayableMedia)new TempMedia(new Metadata(name, format, new TimeSpan(0)), data));
        }
    }
}
