using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Threading.Tasks;
using Suits.Jukebox.Services.Cache;
using System.IO;
using System.Threading;
using Suits.Jukebox.Models.Exceptions;

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

        public override async Task<PlayableMedia> GetMediaAsync()
        {
            using var web = new WebClient();

            var tSrc = new CancellationTokenSource(20000);

            var data = await Task.Run(() => web.DownloadData(mediaUrl), tSrc.Token);
            if (tSrc.IsCancellationRequested)
                throw new CriticalException("Direct media could not be downloaded. (Timer exceeded 20 seconds)");

            var meta = new MediaMetadata() { Title = mediaName, Duration = new TimeSpan(0) };
            meta.DataInformation.Format = mediaFormat;

            return new PlayableMedia(meta, new MemoryStream(data));
        }
    }
}
