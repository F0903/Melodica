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
            if (yt.IsUrlSupported(url))
                return yt;

            if (sp.IsUrlSupported(url))
                return sp;
            return null;
        }

        public Task<bool> VerifyURLAsync(string url);

        public bool IsUrlSupported(string url);

        public Task<(MediaMetadata playlist, IEnumerable<MediaMetadata> videos)> DownloadPlaylistInfoAsync(string url);

        public Task<MediaMetadata> GetMediaInfoAsync(string input);

        public Task<PlayableMedia> DownloadAsync(MediaMetadata info);

        public Task<string> GetLivestreamAsync(string streamURL);
    }
}
