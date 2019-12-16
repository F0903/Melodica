using PokerBot.Models;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace PokerBot.Services.Cache
{
    public interface IAsyncMediaCache
    {
        public void ClearCache();

        public bool ExistsInCache(string key);

        public Task<PlayableMedia> CacheAsync(PlayableMedia media, bool checkCacheSize = true);

        public Task<byte[]> GetCacheAsync(string name);
    }
}
