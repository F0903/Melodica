using System.Collections.Generic;
using System.Threading.Tasks;

using Melodica.Services.Downloaders.YouTube;
using Melodica.Services.Playback.Models;

namespace Melodica.Services.Downloaders
{
    public abstract class AsyncDownloaderBase
    {
        public static readonly AsyncDownloaderBase Default = new AsyncYoutubeDownloader();

        protected abstract Task<MediaType> EvaluateMediaTypeAsync(string url);

        public abstract bool IsUrlSupported(string url);

        public abstract Task<bool> VerifyURLAsync(string url);

        public abstract Task<(MediaMetadata playlist, IEnumerable<MediaMetadata> videos)> DownloadPlaylistInfoAsync(string url);
       
        public abstract bool IsPlaylistAsync(string url);

        public abstract Task<MediaMetadata> GetMediaInfoAsync(string input);

        public abstract Task<PlayableMedia> DownloadAsync(string input);

        public abstract Task<string> GetLivestreamAsync(string streamURL);
    }
}
