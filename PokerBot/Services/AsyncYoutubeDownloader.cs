using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using YoutubeExplode;
using YoutubeExplode.Models.MediaStreams;

namespace PokerBot.Services
{
    public class AsyncYoutubeDownloader : IAsyncDownloadService
    {
        private readonly YoutubeClient yt = new YoutubeClient(new System.Net.Http.HttpClient());

        public Task<string> GetVideoTitleAsync(string query) =>
            Task.FromResult(yt.SearchVideosAsync(query, 1).Result[0].Title);

        public async Task<(Stream stream, string name)> DownloadAsync(string query)
        {
            var vid = (await yt.SearchVideosAsync(query, 1))[0];
            var info = await yt.GetVideoMediaStreamInfosAsync(vid.Id);

            var audioStreams = info.Audio.OrderByDescending(x => x.Bitrate).ToArray();
            if (audioStreams.Length == 0)
                throw new Exception("No streams of media were found.");

            var stream = await yt.GetMediaStreamAsync(audioStreams[0]);
            return (stream, vid.Title);
        }     
        
        public async Task<(string path, string name)> DownloadToCache<T>(T cache, string searchQuery) where T : IAsyncCache<Stream>, new() // Switch to caching interface if there's more cache options
        {
            cache ??= new T();
            var (stream, name) = await DownloadAsync(searchQuery);

            return (await (cache as AsyncFileCache).CacheAsync(stream, name), name);
        }
    }
}
