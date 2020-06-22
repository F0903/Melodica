using Suits.Jukebox.Models;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using YoutubeExplode;

namespace Suits.Jukebox.Services.Downloaders
{
    public interface IAsyncDownloader
    {
        public static readonly IAsyncDownloader Default = new AsyncYoutubeDownloader();

        private static readonly AsyncYoutubeDownloader yt = new AsyncYoutubeDownloader();
        private static readonly AsyncSpotifyDownloader sp = new AsyncSpotifyDownloader();

        public static IAsyncDownloader? GetDownloaderFromURL(string url)
        {
            if (yt.VerifyURLAsync(url).Result)
                return yt;

            if (sp.VerifyURLAsync(url).Result)
                return sp;
            return null;
        }

        public Task<MediaType> EvaluateMediaTypeAsync(string url);

        public Task<bool> VerifyURLAsync(string url);

        public Task<(MediaMetadata playlist, IEnumerable<MediaMetadata> videos)> DownloadPlaylistInfoAsync(string url);

        public Task<MediaMetadata> GetMediaInfoAsync(string url);

        public Task<PlayableMedia> DownloadAsync(string query);

        public Task<string> GetLivestreamAsync(string streamURL);
    }
}
