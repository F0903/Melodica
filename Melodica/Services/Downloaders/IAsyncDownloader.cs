﻿using Melodica.Services.Caching;
using Melodica.Services.Media;

namespace Melodica.Services.Downloaders;

public interface IAsyncDownloader
{
    public bool IsUrlSupported(ReadOnlySpan<char> url);

    public Task<MediaInfo> GetInfoAsync(ReadOnlyMemory<char> query);

    public Task<PlayableMediaStream> DownloadAsync(MediaInfo info);
}
