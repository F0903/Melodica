using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Melodica.Services.Downloaders.YouTube;
using Melodica.Services.Media;

namespace Melodica.Services.Downloaders
{
    public interface IAsyncDownloader
    {
        public static readonly IAsyncDownloader Default = new AsyncYoutubeDownloader();

        public bool IsUrlSupported(string url);

        public Task<MediaInfo> GetInfoAsync(string query);

        public Task<MediaCollection> DownloadAsync(MediaInfo info);
    }
}