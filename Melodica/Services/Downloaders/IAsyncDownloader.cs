using System.Collections.Generic;
using System.Threading.Tasks;

using Melodica.Services.Downloaders.YouTube;
using Melodica.Services.Media;

namespace Melodica.Services.Downloaders
{
    public interface IAsyncDownloader
    {
        public static readonly IAsyncDownloader Default = new AsyncYoutubeDownloader();

        protected Task<MediaType> EvaluateMediaTypeAsync(string url);

        public bool IsUrlSupported(string url);

        public Task<bool> VerifyUrlAsync(string url);

        public Task<(MediaMetadata playlist, IEnumerable<MediaMetadata> videos)> DownloadPlaylistInfoAsync(string url);

        public bool IsUrlPlaylistAsync(string url);

        public Task<MediaMetadata> GetMediaInfoAsync(string input);

        public Task<PlayableMedia> DownloadAsync(string input);

        public Task<PlayableMedia> DownloadAsync(MediaMetadata input);

        public Task<string> GetLivestreamAsync(string streamURL);
    }
}