using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

using Melodica.Core.Exceptions;
using Melodica.Services.Media;
using Melodica.Utility.Extensions;

namespace Melodica.Services.Playback.Requests
{
    public class URLMediaRequest : IMediaRequest
    {
        public URLMediaRequest(string mediaUrl)
        {
            info = new("") { Artist = "External", Title = mediaUrl, Url = mediaUrl };
            remote = mediaUrl;
        }

        private readonly MediaInfo info;
        private readonly string remote;

        public Task<MediaCollection> GetMediaAsync()
        {
            var media = new TempMedia(info, async (_) =>
            {
                using var web = new WebClient();
                var data = await web.DownloadDataTaskAsync(remote);
                var format = remote.AsSpan().ExtractFormatFromFileUrl();
                return new(new MemoryStream(data), format);
            });
            return Task.FromResult(new MediaCollection(media));
        }

        public Task<MediaInfo> GetInfoAsync() => Task.FromResult(info);
    }
}