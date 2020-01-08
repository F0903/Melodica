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

namespace CasinoBot.Modules.Jukebox.Services.Downloaders
{
    public class AsyncYoutubeDownloader : IAsyncDownloadService
    {
        private readonly YoutubeClient yt = new YoutubeClient(new System.Net.Http.HttpClient());

        public const int MaxCacheAttempts = 5;

        public const int MaxDownloadAttempts = 10;

        public const int LargeSizeDurationMinuteThreshold = 20;

        public Task<string> GetVideoTitleAsync(string query) =>
            Task.FromResult(yt.SearchVideosAsync(query, 1).Result[0].Title);

        private async Task<PlayableMedia> InternalDownloadAsync(string query, bool isPreFiltered, Action largeSizeWarningCallback, Action<string> videoUnavailableCallback, int attempt = 0)
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

                return await InternalDownloadAsync(query, false, largeSizeWarningCallback, videoUnavailableCallback, ++attempt).ConfigureAwait(false);
            }

            var stream = await yt.GetMediaStreamAsync(audioStreams[0]);
            return new PlayableMedia(stream, vid.Title, audioStreams[0].Container.ToString().ToLower(), Convert.ToInt32(vid.Duration.TotalSeconds));
        }

        private async Task<PlayableMedia> CacheAsync(IAsyncMediaCache cache, PlayableMedia toCache, string cacheName, bool checkCacheSize, bool ignoreIfContested = false, int attempt = 0)
        {
            if (cache.ExistsInCache(cacheName))
            {
                return cache switch
                {
                    AsyncMediaFileCache fc => await fc.GetValueAsync(cacheName),
                    _ => throw new Exception("Unkown cache type. Please contact owner.")
                };
            }

            var result = await cache.CacheAsync(toCache, checkCacheSize);
            if (result == null)
            {
                if (ignoreIfContested)
                    return null;
                if (attempt >= MaxCacheAttempts)
                    throw new Exception($"Cache attempts exceeded. Could not cache {toCache.Title}");
                await Task.Delay(1000);
                await CacheAsync(cache, toCache, cacheName, checkCacheSize, false, ++attempt);
            }
            return result;
        }

        public async Task<MediaCollection> DownloadToCacheAsync(IAsyncMediaCache cache, QueueMode mode, string guildName, string searchQuery, bool checkCacheSize = true, Action largeSizeWarningCallback = null, Action<string> videoUnavailableCallback = null)
        {
            if (YoutubeClient.TryParsePlaylistId(searchQuery, out var playlistId))
            {
                var pl = await yt.GetPlaylistAsync(playlistId, 1);
                var videos = pl.Videos;

                if (videos.Sum(x => x.Duration.Minutes) > LargeSizeDurationMinuteThreshold)
                    largeSizeWarningCallback?.Invoke();

                PlayableMedia[] med = new PlayableMedia[videos.Count];

                switch (mode)
                {
                    case QueueMode.Consistent:
                        for (int i = 0; i < videos.Count; i++)
                        {
                            var result = await InternalDownloadAsync(videos[i].Id, true, null, videoUnavailableCallback);
                            if (result == null)
                                continue;
                            var cachedResult = await CacheAsync(cache, result, result.Title, false, true);
                            if (cachedResult == null)
                                continue;
                            med[i] = cachedResult;
                        }
                        break;

                    case QueueMode.Fast:
                        Parallel.For(0, videos.Count, new ParallelOptions() { MaxDegreeOfParallelism = -1 }, i =>
                        {
                            PlayableMedia result;
                            result = InternalDownloadAsync(videos[i].Id, true, null, videoUnavailableCallback).Result;
                            if (result == null)
                                return;
                            var cachedResult = CacheAsync(cache, result, result.Title, false, true).Result;
                            if (cachedResult == null)
                                return;
                            med[i] = cachedResult;
                        });
                        break;
                }

                var filterList = med.ToList();
                filterList.RemoveAll(x => x == null);

                //TODO: This could all probably be done more efficient.
                med = filterList.ToArray();

                return new MediaCollection(med, pl.Title, (await Utility.Utility.GetURLArgumentValueAsync<int?>(searchQuery, "index", false)) ?? 1);
            }

            var media = await InternalDownloadAsync(searchQuery, false, largeSizeWarningCallback, videoUnavailableCallback);

            return new MediaCollection(await CacheAsync(cache, media, media.Title, checkCacheSize));
        }
    }
}