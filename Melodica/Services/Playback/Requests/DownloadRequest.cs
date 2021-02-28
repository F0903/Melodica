using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Melodica.Services.Downloaders;
using Melodica.Services.Downloaders.Exceptions;
using Melodica.Services.Downloaders.YouTube;
using Melodica.Services.Media;

namespace Melodica.Services.Playback.Requests
{
    public class DownloadRequest : MediaRequest
    {
        public DownloadRequest(string query) : this(query, new AsyncYoutubeDownloader())
        {
        }

        public DownloadRequest(string query, IAsyncDownloader dl)
        {
            downloader = dl;
            this.query = query;

            
        }

        private DownloadRequest(MediaInfo info, MediaInfo parentRequestInfo, IAsyncDownloader dl)
        {
            this.info = info;
            query = info.Url!;

            ParentRequestInfo = parentRequestInfo;

            downloader = dl;
            this.info = info;
        }

        private readonly IAsyncDownloader downloader;

        public override MediaInfo GetInfo() => ;

        public override async Task<MediaCollection> GetMediaAsync()
        {
            
        }
    }
}