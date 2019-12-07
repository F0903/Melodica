using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using YoutubeExplode;
using YoutubeExplode.Models.MediaStreams;
using PokerBot.Utility.Extensions;

namespace PokerBot.Services
{
    public class AsyncYoutubeDownloader : IAsyncDownloadService
    {
        private readonly YoutubeClient yt = new YoutubeClient(new System.Net.Http.HttpClient());

        public Task<string> GetVideoTitleAsync(string query) =>
            Task.FromResult(yt.SearchVideosAsync(query, 1).Result[0].Title);

        public async Task<(Stream stream, string name)> DownloadAsync(string query)
        {
            var vid = query.IsUrl() ? (await yt.GetVideoAsync(YoutubeClient.ParseVideoId(query))) : (await yt.SearchVideosAsync(query, 1))[0];

            var info = await yt.GetVideoMediaStreamInfosAsync(vid.Id);

            var audioStreams = info.Audio.OrderByDescending(x => x.Bitrate).ToArray();
            if (audioStreams.Length == 0)
                throw new Exception("No streams of media were found.");

            var stream = await yt.GetMediaStreamAsync(audioStreams[0]);
            return (stream, vid.Title);
        }

        public async Task<(string path, string name)> DownloadToCache<T>(T cache, string searchQuery) where T : IAsyncCache<Stream>, new() // Switch to caching interface if there's more cache options
        {
            if (cache != null)
            {
                var title = await GetVideoTitleAsync(searchQuery);
                if (cache.ExistsInCache(title))
                {
                    if(cache is AsyncFileCache fc)
                    {
                        return (await fc.GetValueAsync(title), title);
                    }

                    return (null, title);
                }
            }

            cache = new T();
            var (stream, name) = await DownloadAsync(searchQuery);

            return (await (cache as AsyncFileCache).CacheAsync(stream, name, false), name);
        }
    }
}
