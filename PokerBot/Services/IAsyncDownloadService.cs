using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace PokerBot.Services
{
    public interface IAsyncDownloadService
    {
        public Task<DownloadResult> DownloadAsync(string query);
    }
}
