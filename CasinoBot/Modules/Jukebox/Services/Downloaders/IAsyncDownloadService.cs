using CasinoBot.Modules.Jukebox.Models;
using CasinoBot.Modules.Jukebox.Services.Cache;
using System;
using System.Threading.Tasks;

namespace CasinoBot.Modules.Jukebox.Services.Downloaders
{
    public enum QueueMode { Consistent, Fast }

    public interface IAsyncDownloadService
    {     
        public Task<MediaCollection> DownloadToCacheAsync(IAsyncMediaCache cache, QueueMode mode, string guildName, string searchQuery, bool checkCacheSize = true, Action largeSizeWarningCallback = null, Action<string> videoUnavailableCallback = null);
    }
}