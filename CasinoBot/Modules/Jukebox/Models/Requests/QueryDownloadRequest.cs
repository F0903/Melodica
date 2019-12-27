using CasinoBot.Modules.Jukebox.Services.Downloaders;

namespace CasinoBot.Modules.Jukebox.Models.Requests
{
    public class QueryDownloadRequest : IRequest
    {
        public QueryDownloadRequest(IAsyncDownloadService downloader, string requestQuery)
        {
            this.downloader = downloader;
            this.requestQuery = requestQuery;
        }

        public bool IsDownloadRequest { get; } = true;

        private readonly IAsyncDownloadService downloader;

        private readonly string requestQuery;

        public object GetRequest()
        {
            return requestQuery;
        }

        public IAsyncDownloadService GetDownloader()
        {
            return downloader;
        }
    }
}
