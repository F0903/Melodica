using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace PokerBot.Services
{
    public interface IAsyncDownloadService
    {
        public Task<(Stream stream, string name)> DownloadAsync(string query);
    }
}
