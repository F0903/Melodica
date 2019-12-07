using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace PokerBot.Services.Cache
{
    public interface IAsyncCache<T>
    {
        public void ClearCache();

        public bool ExistsInCache(string key);

        public Task<(string path, string format)> CacheAsync(T val, string cacheName, string format, bool checkCacheSize = true);

        public Task<byte[]> GetCacheAsync(string name);
    }
}
