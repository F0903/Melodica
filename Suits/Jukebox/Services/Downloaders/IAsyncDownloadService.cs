using Suits.Jukebox.Models;
using Suits.Jukebox.Services.Cache;
using System;
using System.Threading.Tasks;

namespace Suits.Jukebox.Services.Downloaders
{
    public enum QueueMode { Consistent, Fast }

    public interface IAsyncDownloadService
    {
        public Action? LargeSizeWarningCallback { get; set; }
        public Action<string>? VideoUnavailableCallback { get; set; }

        public Task<string> GetMediaTitleAsync(string query);

        public Task<MediaCollection> DownloadAsync(string searchQuery);
    }
}