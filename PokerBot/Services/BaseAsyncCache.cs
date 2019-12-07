using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace PokerBot.Services
{
    public interface IAsyncCache<T>
    {
        public void ClearCache();

        public bool ExistsInCache(string key);

        public Task<string> CacheAsync(T val, string cacheName, bool checkCacheSize);

        public Task<byte[]> GetCacheAsync(string name);
    }
}
