using PokerBot.Modules.Jukebox.Models;
using PokerBot.Modules.Jukebox.Services.Cache;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace PokerBot.Modules.Jukebox.Services.Downloaders
{
    public interface IAsyncDownloadService
    {
        public Task<MediaCollection> DownloadAsync(IAsyncMediaCache cache, string searchQuery, bool checkCacheSize = true, Action largeSizeWarningCallback = null, Action<string> videoUnavailableCallback = null);
    }
}
