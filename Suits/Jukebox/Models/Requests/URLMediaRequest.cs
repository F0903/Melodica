using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Threading.Tasks;
using Suits.Jukebox.Services;
using System.IO;
using System.Threading;
using Suits.Jukebox.Models.Exceptions;

namespace Suits.Jukebox.Models.Requests
{
    public class URLMediaRequest : MediaRequestBase
    {
        public URLMediaRequest(string? mediaName, string mediaUrl, bool directStream)
        {
            this.mediaFormat = Utility.General.GetUrlResourceFormat(mediaUrl);
            this.mediaName = mediaName ?? $"External {mediaFormat.ToUpper()} {(directStream ? "Stream" : "File")}";
            this.mediaUrl = mediaUrl;
            this.directStream = directStream;

            RequestMediaType = directStream ? MediaType.Livestream : MediaType.Video;
            info = new MediaMetadata()
            {
                MediaType = MediaType.Livestream,
                URL = this.mediaUrl,
                Title = this.mediaName,
                DataInformation = new MediaMetadata.DataInfo()
                {
                    Format = mediaFormat,
                    MediaPath = this.mediaUrl
                }
            };
        }

        public override MediaType RequestMediaType { get; protected set; }
        public override SubRequestInfo? SubRequestInfo { get; protected set; }
        public override List<MediaRequestBase>? SubRequests { get; set; }


        private readonly string mediaName;

        private readonly string mediaFormat;

        private readonly string mediaUrl;

        private readonly bool directStream;

        private readonly MediaMetadata info;

        private async Task<PlayableMedia> DownloadMediaAsync()
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

        public override Task<PlayableMedia> GetMediaAsync()
        {
            if (directStream)
            {
                return Task.FromResult(new PlayableMedia(info, null));
            }
            else
            {
                return DownloadMediaAsync();
            }
        }

        public override MediaMetadata GetInfo() => info;
    }
}
