using Suits.Jukebox.Services.Cache;
using Suits.Jukebox.Services.Downloaders;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Suits.Jukebox.Models.Requests
{
    public class DownloadMediaRequest : MediaRequest
    {
        public DownloadMediaRequest(IAsyncDownloadService downloader, string query)
        {
            this.downloader = downloader;
            this.query = query;
        }

        private readonly IAsyncDownloadService downloader;

        private readonly string query;

        public override Task<MediaCollection> GetMediaRequestAsync()
        {
            return downloader.DownloadAsync(query);
        }
    }
}
