using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace PokerBot.Services
{
    public interface IAsyncCachingService
    {

        public void ClearCache();

        public bool ExistsInCache(string name);

        public Task<string> CacheAsync((Stream stream, string name) result);

        public Task<byte[]> GetCacheAsync(string name);
    }
}
