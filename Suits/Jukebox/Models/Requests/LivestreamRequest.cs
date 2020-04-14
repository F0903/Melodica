using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Suits.Jukebox.Models.Requests
{
    // This seems smelly
    public abstract class BaseLivestreamRequest
    {
        public abstract Task<string> GetHLSUrlAsync();
        public abstract Task<IMediaInfo> GetInfoAsync();
    }

    public class LivestreamRequest<Downloader> : BaseLivestreamRequest where Downloader : class, Services.Downloaders.IAsyncDownloader, new()
    {
        public LivestreamRequest(string url, Downloader? dl = null)
        {
            downloader = dl ?? new Downloader();
            videoUrl = url;
            info = downloader.GetMediaInfoAsync(url).Result;
        }

        private readonly Downloader downloader;

        private readonly string videoUrl;

        private readonly IMediaInfo info;

        public override Task<string> GetHLSUrlAsync()
        {
            return downloader.GetLivestreamAsync(videoUrl);
        }

        public override Task<IMediaInfo> GetInfoAsync() => Task.FromResult(info);
    }
}
