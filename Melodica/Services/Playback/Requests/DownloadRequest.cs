﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Melodica.Services.Downloaders;
using Melodica.Services.Downloaders.Exceptions;
using Melodica.Services.Downloaders.YouTube;
using Melodica.Services.Media;

namespace Melodica.Services.Playback.Requests
{
    public class DownloadRequest : IMediaRequest
    {
        public DownloadRequest(string query) : this(query, new AsyncYoutubeDownloader())
        { }

        public DownloadRequest(string query, IAsyncDownloader dl)
        {
            downloader = dl;
            this.query = query;
        }

        readonly string query;

        readonly IAsyncDownloader downloader;

        MediaInfo? cachedInfo;

        public async Task<MediaInfo> GetInfoAsync() => cachedInfo ??= await downloader.GetInfoAsync(query);

        public async Task<MediaCollection> GetMediaAsync()
        {
            var info = await GetInfoAsync();
            return await downloader.DownloadAsync(info);
        }
    }
}