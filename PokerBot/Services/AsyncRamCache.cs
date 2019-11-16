using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace PokerBot.Services
{
    public class AsyncRamCache : IAsyncCachingService
    {
        private readonly Dictionary<string, byte[]> cache = new Dictionary<string, byte[]>();
        
        public void ClearCache() =>
            cache.Clear();

        public bool ExistsInCache(string name) =>
            cache.ContainsKey(name);

        public async Task<string> CacheAsync((Stream stream, string name) result)
        {
            if (ExistsInCache(result.name))
                return result.name;

            using var stream = result.stream;

            using var membuf = new MemoryStream();

            await stream.CopyToAsync(membuf, 80 * 1024);

            cache.Add(result.name, membuf.ToArray());
            return result.name;
        }
        
        public Task<byte[]> GetCacheAsync(string name)
        {
            if (!cache.TryGetValue(name, out var val))
                throw new Exception($"Could not get cache value. Name: {name}");
            return Task.FromResult(val);
        }
    }
}
