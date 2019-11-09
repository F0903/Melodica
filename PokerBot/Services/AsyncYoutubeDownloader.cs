using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using YoutubeExplode;

namespace PokerBot.Services
{
    public class AsyncYoutubeDownloader : IAsyncDownloadService
    {
        private readonly YoutubeClient yt = new YoutubeClient(new System.Net.Http.HttpClient());

        public async Task<DownloadResult> DownloadAsync(string query)
        {
            var vid = (await yt.SearchVideosAsync(query, 1))[0];
            var info = await yt.GetVideoMediaStreamInfosAsync(vid.Id);
            var stream = await yt.GetMediaStreamAsync(info.Audio.OrderByDescending(x => x.Bitrate).ToArray()[0]);
            return new DownloadResult(stream, vid.Title);
        }

    }

    public struct DownloadResult
    {
        public DownloadResult(Stream stream, string name)
        {
            Stream = stream;
            Name = name;
        }

        public Stream Stream { get; }
        public string Name { get; }
    }
}
