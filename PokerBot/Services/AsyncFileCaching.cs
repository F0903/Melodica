using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using PokerBot.Utility.Extensions;

namespace PokerBot.Services
{
    public class AsyncFileCaching : IAsyncCachingService
    {
        public bool ClearCache()
        {
            if (!Directory.Exists("mediacache"))
                return true;

            foreach (var file in Directory.EnumerateFiles("mediacache"))
                File.Delete(file);
            return true;
        }

        public async Task<string> CacheAsync(DownloadResult result)
        {
            using var stream = result.Stream;

            string path = $"mediacache/{result.Name.RemoveSpecialCharacters()}.mp3";

            if (File.Exists(path))
                return path;

            using var file = File.Create(path);
            await stream.CopyToAsync(file);
            return path;
        }
    }
}
