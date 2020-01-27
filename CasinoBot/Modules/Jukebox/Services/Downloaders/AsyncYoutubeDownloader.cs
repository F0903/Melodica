using CasinoBot.Modules.Jukebox.Models;
using CasinoBot.Modules.Jukebox.Services.Cache;
using CasinoBot.Utility.Extensions;
using System;
using System.Linq;
using YoutubeExplode;
using YoutubeExplode.Models;
using YoutubeExplode.Exceptions;
using YoutubeExplode.Models.MediaStreams;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;
using System.IO;

namespace CasinoBot.Modules.Jukebox.Services.Downloaders
{
    public class AsyncYoutubeDownloader : IAsyncDownloadService
    {
        private readonly YoutubeClient yt = new YoutubeClient(new System.Net.Http.HttpClient());

        public const int MaxCacheAttempts = 5;

        public const int MaxDownloadAttempts = 10;

        public const int LargeSizeDurationMinuteThreshold = 20;

        public const int MaxPlaylistVideos = 100;

        public Task<string> GetMediaTitleAsync(string query) =>
            Task.FromResult(yt.SearchVideosAsync(query, 1).Result[0].Title);

        private async Task<PlayableMedia> InternalDownloadAsync(string query, bool isPreFiltered, Action largeSizeWarningCallback, Action<string> videoUnavailableCallback, bool toQueue = false, int attempt = 0)
        {
            bool isQueryUrl = query.IsUrl();

            var vid = isQueryUrl || isPreFiltered ? (await yt.GetVideoAsync(isPreFiltered ? query : YoutubeClient.ParseVideoId(query))) : (await yt.SearchVideosAsync(query, 1))[attempt];

            if (vid.Duration.Minutes > LargeSizeDurationMinuteThreshold)
                largeSizeWarningCallback?.Invoke();

            MediaStreamInfoSet info;
            try
            {
                info = await yt.GetVideoMediaStreamInfosAsync(vid.Id);
            }
            catch (Exception ex) when (ex is VideoUnplayableException || ex is VideoUnavailableException)
            {
                videoUnavailableCallback?.Invoke(vid.Title);
                return null;
            }

            var audioStreams = info.Audio.OrderByDescending(x => x.Bitrate).ToArray();
            if (audioStreams.Length == 0)
            {
                if (isQueryUrl)
                    throw new Exception("This video does not have have available media streams.");

                if (attempt > MaxDownloadAttempts)
                    throw new Exception($"No videos with available media streams could be found. Attempts: {attempt}/{MaxDownloadAttempts}");

                return await InternalDownloadAsync(query, false, largeSizeWarningCallback, videoUnavailableCallback, toQueue, ++attempt).ConfigureAwait(false);
            }
            var stream = await yt.GetMediaStreamAsync(audioStreams[0]);
            return new PlayableMedia(new Metadata(vid.Title.ReplaceIllegalCharacters(), audioStreams[0].Container.ToString().ToLower(), vid.Duration), stream.ToBytes());
        }

        private Task<MediaCollection> CacheAsync(MediaCollection col, MediaCache cache, bool pruneCache = true)
        {
            return cache.CacheMediaAsync(col, pruneCache || !col.IsPlaylist);
        }

        public async Task<MediaCollection> DownloadToCacheAsync(MediaCache cache, QueueMode mode, Discord.IGuild guild, string searchQuery, bool pruneCache = true, Action largeSizeWarningCallback = null, Action<string> videoUnavailableCallback = null)
        {
            // Refactor this whole shite.
            if (YoutubeClient.TryParsePlaylistId(searchQuery, out var playlistId))
            {
                var pl = await yt.GetPlaylistAsync(playlistId, 1);
                var videos = pl.Videos;

                var plIndex = await Utility.Utility.GetURLArgumentIntValueAsync(searchQuery, "index", false) ?? 1;

                if (videos.Sum(x => x.Duration.Minutes) > LargeSizeDurationMinuteThreshold)
                    largeSizeWarningCallback?.Invoke();

                var num = videos.Count > MaxPlaylistVideos ? MaxPlaylistVideos : videos.Count;

                PlayableMedia[] pList = new PlayableMedia[num];
                switch (mode)
                {
                    case QueueMode.Consistent:
                        for (int i = 0; i < num; i++)
                        {
                            if (cache.Contains(videos[i].Title))
                            {
                                pList[i] = await cache.GetAsync(videos[i].Title);
                                continue;
                            }
                            var result = await InternalDownloadAsync(videos[i].Id, true, null, videoUnavailableCallback);
                            if (result == null)
                                continue;
                            pList[i] = result;
                        }
                        break;

                    case QueueMode.Fast:
                        Parallel.For(0, num, new ParallelOptions() { MaxDegreeOfParallelism = -1 }, i =>
                        {
                            if (cache.Contains(videos[i].Title))
                            {
                                pList[i] = cache.GetAsync(videos[i].Title).Result;
                                return;
                            }
                            var result = InternalDownloadAsync(videos[i].Id, true, null, videoUnavailableCallback).Result;
                            if (result == null)
                                return;
                            pList[i] = result;
                        });
                        break;
                }

                var pListFilter = pList.ToList();
                pListFilter.RemoveAll(x => x == null);
                pList = pListFilter.ToArray();

                var cached = await cache.CacheMediaAsync(new MediaCollection(pList, pl.Title));

                return cached;
            }

            var title = await GetMediaTitleAsync(searchQuery);
            if (cache.Contains(title))
            {
                return await cache.GetAsync(title);
            }

            var media = await InternalDownloadAsync(searchQuery, false, largeSizeWarningCallback, videoUnavailableCallback);

            return await CacheAsync(media, cache);
        }       
    }
}