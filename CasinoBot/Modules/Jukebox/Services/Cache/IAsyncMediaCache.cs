using CasinoBot.Modules.Jukebox.Models;
using System.Threading.Tasks;

namespace CasinoBot.Modules.Jukebox.Services.Cache
{
    public interface IAsyncMediaCache
    {
        public Task Init(string guildName);

        public void ClearCache();

        public bool ExistsInCache(string key);

        public Task<PlayableMedia> CacheAsync(PlayableMedia media, bool checkCacheSize = true);

        public Task<byte[]> GetCacheAsync(string name);
    }
}