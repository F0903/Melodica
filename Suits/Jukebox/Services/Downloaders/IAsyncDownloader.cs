using Suits.Jukebox.Models;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Suits.Jukebox.Services.Downloaders
{
    public interface IAsyncDownloader
    {
        public Task<bool> IsPlaylistAsync(string url);

        public Task<bool> VerifyURLAsync(string url);

        public Task<IEnumerable<string>> GetPlaylistVideoURLsAsync(string url);

        public Task<IMediaInfo> GetMediaInfoAsync(string url);

        public Task<MediaCollection> DownloadAsync(string query);
    }
}
