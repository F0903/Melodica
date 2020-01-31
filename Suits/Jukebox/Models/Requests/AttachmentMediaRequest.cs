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
        public AttachmentMediaRequest(Discord.Attachment[] attachments, MediaCache cache)
        {
            this.cache = cache;
            this.attachments = attachments;
        }

        readonly MediaCache cache;

        readonly Discord.Attachment[] attachments;

        public override Task<MediaCollection> GetMediaRequestAsync()
        {
            using var web = new WebClient();
            List<PlayableMedia> media = new List<PlayableMedia>();
            foreach (var item in attachments)
            {
                var data = web.DownloadData(item.Url);
                var name = item.Filename;
                var format = Path.GetExtension(name).Replace(".", "");
                media.Add(new TempMedia(new Metadata(name, format, new TimeSpan(0)), data, cache));
            }
            return Task.FromResult(media.Count > 1 ? new MediaCollection(media, "Attachments") : new MediaCollection(media.First()));
        }
    }
}
