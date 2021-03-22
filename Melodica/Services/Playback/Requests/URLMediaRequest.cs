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
    //TODO:
    public class URLMediaRequest : IMediaRequest
    {
        public URLMediaRequest(string? mediaName, string mediaUrl, bool directStream)
        {
            throw new NotImplementedException();
        }

        private readonly MediaInfo info;

        private async Task<MediaCollection> DownloadMediaAsync()
        {
            throw new NotImplementedException();
        }

        public Task<MediaCollection> GetMediaAsync()
        {
            throw new NotImplementedException();
        }

        public Task<MediaInfo> GetInfoAsync() => Task.FromResult(info);
    }
}