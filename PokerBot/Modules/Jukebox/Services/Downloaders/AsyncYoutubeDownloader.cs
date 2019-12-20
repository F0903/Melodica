using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using YoutubeExplode;
using YoutubeExplode.Models.MediaStreams;
using PokerBot.Utility.Extensions;
using PokerBot.Modules.Jukebox.Services.Cache;
using PokerBot.Modules.Jukebox.Models;
using YoutubeExplode.Models;
using System.Collections;
using System.Runtime.InteropServices;

namespace PokerBot.Modules.Jukebox.Services.Downloaders
{   
    public class AsyncYoutubeDownloader : IAsyncDownloadService
    {
        private readonly YoutubeClient yt = new YoutubeClient(new System.Net.Http.HttpClient());

        public const int MaxDownloadAttempts = 10;

        public Task<string> GetVideoTitleAsync(string query) =>
            Task.FromResult(yt.SearchVideosAsync(query, 1).Result[0].Title);

        private async Task<PlayableMedia> InternalDownloadAsync(string query, int attempt = 0)
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
            return new PlayableMedia(stream, vid.Title, audioStreams[0].Container.ToString().ToLower());
        }

        private async Task<PlayableMedia> CacheAsync(IAsyncMediaCache cache, PlayableMedia toCache, string cacheName, bool checkCacheSize)
        {
            if (cache.ExistsInCache(cacheName)) 
            {
                return cache switch
                {
                    AsyncMediaFileCache fc => await fc.GetValueAsync(cacheName),
                    _ => throw new Exception("Unkown cache type. Please contact owner.")
                };              
            }

            return await cache.CacheAsync(toCache, checkCacheSize);
        }

        public async Task<MediaCollection> DownloadAsync(IAsyncMediaCache cache, string searchQuery, bool checkCacheSize = true)
        {
            if (YoutubeClient.TryParsePlaylistId(searchQuery, out var playlistId))
            {
                var pl = await yt.GetPlaylistAsync(playlistId, 1);

                List<PlayableMedia> med = new List<PlayableMedia>();
                foreach (var vid in pl.Videos)
                    med.Add((await DownloadAsync(cache, vid.Id, false))[0]);

                return new MediaCollection(med.ToArray(), pl.Title, await PokerBot.Utility.Utility.GetURLArgumentValueAsync<int>(searchQuery, "index"));
            }

            var title = await GetVideoTitleAsync(searchQuery);

            var media = await InternalDownloadAsync(searchQuery);

            return new MediaCollection(await CacheAsync(cache, media, title, checkCacheSize));
        }       
    }    
}
