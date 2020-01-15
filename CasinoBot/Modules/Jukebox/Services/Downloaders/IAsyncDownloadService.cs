using CasinoBot.Modules.Jukebox.Models;
using CasinoBot.Modules.Jukebox.Services.Cache;
using System;
using System.Threading.Tasks;

namespace CasinoBot.Modules.Jukebox.Services.Downloaders
{
    public enum QueueMode { Consistent, Fast }

    public interface IAsyncDownloadService
    {
        public Task<MediaCollection> DownloadToCacheAsync(MediaCache cache, QueueMode mode, Discord.IGuild guild, string searchQuery, bool pruneCache = true, Action largeSizeWarningCallback = null, Action<string> videoUnavailableCallback = null);
    }
}