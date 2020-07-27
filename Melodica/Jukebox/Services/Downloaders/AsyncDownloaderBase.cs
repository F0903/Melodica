using Melodica.Jukebox.Models;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using YoutubeExplode;

namespace Melodica.Jukebox.Services.Downloaders
{
    public abstract class AsyncDownloaderBase
    {
        public static readonly AsyncDownloaderBase Default = new AsyncYoutubeDownloader();

        protected abstract Task<MediaType> EvaluateMediaTypeAsync(string url);

        public abstract Task<bool> VerifyURLAsync(string url);

        public abstract Task<(MediaMetadata playlist, IEnumerable<MediaMetadata> videos)> DownloadPlaylistInfoAsync(string url);
       
        public abstract bool IsPlaylistAsync(string url);

        public abstract Task<MediaMetadata> GetMediaInfoAsync(string input);

        public abstract Task<PlayableMedia> DownloadAsync(string input);

        public abstract Task<PlayableMedia> DownloadToExistingMetaAsync(MediaMetadata meta);

        public abstract Task<string> GetLivestreamAsync(string streamURL);
    }
}
