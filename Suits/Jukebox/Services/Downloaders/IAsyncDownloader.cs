using Suits.Jukebox.Models;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Suits.Jukebox.Services.Downloaders
{
    public interface IAsyncDownloader
    {
        public Task<MediaType> EvaluateMediaTypeAsync(string url);

        public Task<bool> VerifyURLAsync(string url);

        public Task<(Metadata playlist, IEnumerable<Metadata> videos)> DownloadPlaylistInfoAsync(string url);

        public Task<Metadata> GetMediaInfoAsync(string url);

        public Task<MediaCollection> DownloadAsync(string query);

        public Task<string> GetLivestreamAsync(string streamURL);
    }
}
