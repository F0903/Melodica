using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace PokerBot.Services
{
    public interface IAsyncCachingService
    {
        public bool ClearCache();
        public Task<string> CacheAsync(DownloadResult result);
    }
}
