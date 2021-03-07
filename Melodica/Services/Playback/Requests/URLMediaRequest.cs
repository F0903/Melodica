using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

using Melodica.Core.Exceptions;
using Melodica.Services.Media;

namespace Melodica.Services.Playback.Requests
{
    public class URLMediaRequest : IMediaRequest
    {
        public URLMediaRequest(string? mediaName, string mediaUrl, bool directStream)
        {
            mediaFormat = Utility.Utils.GetUrlResourceFormat(mediaUrl);
            this.mediaName = mediaName ?? $"External {mediaFormat.ToUpper()} {(directStream ? "Stream" : "File")}";
            this.mediaUrl = mediaUrl;
            this.directStream = directStream;

            info = new MediaInfo()
            {
                MediaType = directStream ? MediaType.Livestream : MediaType.Video,
                Url = this.mediaUrl,
                Title = this.mediaName,
                DataInformation = new DataInfo()
                {
                    Format = mediaFormat,
                    MediaPath = this.mediaUrl
                }
            };
        }

        private readonly string mediaName;

        private readonly string mediaFormat;

        private readonly string mediaUrl;

        private readonly bool directStream;

        private readonly MediaInfo info;

        private async Task<MediaCollection> DownloadMediaAsync()
        {
            using var web = new WebClient();

            var tSrc = new CancellationTokenSource(20000);

            byte[]? data = await Task.Run(() => web.DownloadData(mediaUrl), tSrc.Token);
            if (tSrc.IsCancellationRequested)
                throw new CriticalException("Direct media could not be downloaded. (Timer exceeded 20 seconds)");

            var meta = new MediaInfo() { Title = mediaName, Duration = new TimeSpan(0) };
            meta.DataInformation.Format = mediaFormat;

            return new MediaCollection(new PlayableMedia(meta, (_) => Task.FromResult(((Stream)new MemoryStream(data), ""))));
        }

        public Task<MediaCollection> GetMediaAsync()
        {
            if (directStream)
            {
                return Task.FromResult(new MediaCollection(new PlayableMedia(info, null)));
            }
            else
            {
                return DownloadMediaAsync();
            }
        }

        public Task<MediaInfo> GetInfoAsync() => Task.FromResult(info);
    }
}