using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Threading.Tasks;
using Suits.Jukebox.Services.Cache;

namespace Suits.Jukebox.Models.Requests
{
    public class URLMediaRequest : MediaRequest
    {
        public URLMediaRequest(string mediaName, string mediaFormat, string mediaUrl)
        {
            this.mediaName = mediaName;
            this.mediaFormat = mediaFormat;
            this.mediaUrl = mediaUrl;
        }

        private readonly string mediaName;

        private readonly string mediaFormat;

        private readonly string mediaUrl;

        public override Task<PlayableMedia> GetMediaAsync()
        {
            using var web = new WebClient();

            var data = web.DownloadData(mediaUrl);

            return Task.FromResult(MediaCache.CacheMediaAsync(new PlayableMedia(new Metadata(new MediaInfo() { Title = mediaName, Duration = new TimeSpan(0) }, mediaFormat), data)).Result);
        }
    }
}
