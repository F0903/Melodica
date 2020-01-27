using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Threading.Tasks;
using CasinoBot.Modules.Jukebox.Services.Cache;

namespace CasinoBot.Modules.Jukebox.Models.Requests
{
    public class CustomMediaRequest : MediaRequest
    {
        public CustomMediaRequest(string mediaName, string mediaFormat, string mediaUrl, MediaCache cache)
        {
            this.mediaName = mediaName;
            this.mediaFormat = mediaFormat;
            this.mediaUrl = mediaUrl;
            this.cache = cache;
        }

        private readonly string mediaName;

        private readonly string mediaFormat;

        private readonly string mediaUrl;

        private readonly MediaCache cache;

        public override Task<MediaCollection> GetMediaRequestAsync()
        {
            using var web = new WebClient();

            var data = web.DownloadData(mediaUrl);

            return cache.CacheMediaAsync(new PlayableMedia(new Metadata(mediaName, mediaFormat, new TimeSpan(0)), data));
        }
    }
}
