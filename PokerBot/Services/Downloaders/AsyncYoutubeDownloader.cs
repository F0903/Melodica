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

namespace PokerBot.Services.Downloaders
{
    public class AsyncYoutubeDownloader : IAsyncDownloadService
    {
        private readonly YoutubeClient yt = new YoutubeClient(new System.Net.Http.HttpClient());

        public Task<string> GetVideoTitleAsync(string query) =>
            Task.FromResult(yt.SearchVideosAsync(query, 1).Result[0].Title);

        public async Task<(Stream stream, string name, string format)> DownloadAsync(string query)
        {
            var vid = query.IsUrl() ? (await yt.GetVideoAsync(YoutubeClient.ParseVideoId(query))) : (await yt.SearchVideosAsync(query, 1))[0];

            var info = await yt.GetVideoMediaStreamInfosAsync(vid.Id);

            var audioStreams = info.Audio.OrderByDescending(x => x.Bitrate).ToArray();
            if (audioStreams.Length == 0)
                throw new Exception("No streams of media were found.");

            var stream = await yt.GetMediaStreamAsync(audioStreams[0]);
            return (stream, vid.Title, audioStreams[0].Container.ToString().ToLower());
        }

        public async Task<(string path, string name, string format)> DownloadToCache<T>(T cache, string searchQuery, bool checkCacheSize = true) where T : IAsyncCache<Stream>, new() // Switch to caching interface if there's more cache options
        {
            if (cache != null)
            {
                var title = await GetVideoTitleAsync(searchQuery);
                if (cache.ExistsInCache(title))
                {
                    if(cache is AsyncFileCache fc)
                    {
                        var val = await fc.GetValueAsync(title);
                        return (val.path, title, val.format);
                    }

                    return (null, title, null);
                }
            }

            cache = new T();
            var (stream, name, format) = await DownloadAsync(searchQuery);

            var result = await (cache as AsyncFileCache).CacheAsync(stream, name, format, checkCacheSize);

            return (result.path, name, result.format);
        }
    }
}
