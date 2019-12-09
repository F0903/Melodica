using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using YoutubeExplode;
using YoutubeExplode.Models.MediaStreams;
using PokerBot.Utility.Extensions;
using PokerBot.Services.Cache;
using YoutubeExplode.Models;
using System.Collections;
using PokerBot.Models;

namespace PokerBot.Services.Downloaders
{
    public class AsyncYoutubeDownloader : IAsyncDownloadService
    {
        private readonly YoutubeClient yt = new YoutubeClient(new System.Net.Http.HttpClient());

        public const int MaxDownloadAttempts = 10;

        public Task<string> GetVideoTitleAsync(string query) =>
            Task.FromResult(yt.SearchVideosAsync(query, 1).Result[0].Title);

        private async Task<(Stream stream, string name, string format)> InternalDownloadAsync(string query, int attempt = 0)
        {
            bool isQueryUrl = query.IsUrl();
            var vid = isQueryUrl ? (await yt.GetVideoAsync(YoutubeClient.ParseVideoId(query))) : (await yt.SearchVideosAsync(query, 1))[attempt];

            var info = await yt.GetVideoMediaStreamInfosAsync(vid.Id);

            var audioStreams = info.Audio.OrderByDescending(x => x.Bitrate).ToArray();
            if (audioStreams.Length == 0)
            {
                if (isQueryUrl)
                    throw new Exception("This video does not have have available media streams.");

                if (attempt > MaxDownloadAttempts)
                    throw new Exception($"No videos with available media streams could be found. Attempts: {attempt}/{MaxDownloadAttempts}");

                return await InternalDownloadAsync(query, ++attempt).ConfigureAwait(false);
            }

            var stream = await yt.GetMediaStreamAsync(audioStreams[0]);
            return (stream, vid.Title, audioStreams[0].Container.ToString().ToLower());
        }

        public async Task<DownloadResult> DownloadAsync<T>(T cache, string searchQuery, bool checkCacheSize = true) where T : IAsyncCache<Stream>, new() // Switch to caching interface if there's more cache options
        {
            if (YoutubeClient.TryParsePlaylistId(searchQuery, out var playlistId))
            {
                // TODO: Add playlist support
                throw new NotImplementedException();
            }

            if (cache != null)
            {
                var title = await GetVideoTitleAsync(searchQuery);
                if (cache.ExistsInCache(title))
                {
                    if (cache is AsyncFileCache fc)
                    {
                        var cacheResult = await fc.GetValueAsync(title);
                        return new DownloadResult(title, cacheResult.path, cacheResult.format);
                    }

                    return new DownloadResult(title, null, null);
                }
            }

            cache = new T();
            var (stream, name, format) = await InternalDownloadAsync(searchQuery);

            var result = await (cache as AsyncFileCache).CacheAsync(stream, name, format, checkCacheSize);

            return new DownloadResult(name, result.path, format);
        }       
    }    
}
