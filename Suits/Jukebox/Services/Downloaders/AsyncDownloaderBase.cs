using Suits.Jukebox.Models;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using YoutubeExplode;

namespace Suits.Jukebox.Services.Downloaders
{
    public abstract class AsyncDownloaderBase
    {
        public static readonly AsyncDownloaderBase Default = new AsyncYoutubeDownloader();

        public abstract Task<bool> VerifyURLAsync(string url);

        public abstract bool IsUrlSupported(string url);

        public abstract Task<(MediaMetadata playlist, IEnumerable<MediaMetadata> videos)> DownloadPlaylistInfoAsync(string url);

        public abstract Task<MediaMetadata> GetMediaInfoAsync(string input);

        public abstract Task<PlayableMedia> DownloadAsync(MediaMetadata info);

        public abstract Task<string> GetLivestreamAsync(string streamURL);
    }
}
