﻿using PokerBot.Models;
using PokerBot.Services.Cache;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace PokerBot.Services.Downloaders
{
    public interface IAsyncDownloadService
    {
        public Task<MediaCollection> DownloadAsync(IAsyncMediaCache cache, string searchQuery, bool checkCacheSize = true);
    }
}
