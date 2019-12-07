using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace PokerBot.Services.Downloaders
{
    public interface IAsyncDownloadService
    {
        public Task<(Stream stream, string name, string format)> DownloadAsync(string query);
    }
}
