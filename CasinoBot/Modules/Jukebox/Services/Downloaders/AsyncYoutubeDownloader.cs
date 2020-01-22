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

        public Task<string> GetMediaTitleAsync(string query) =>
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
            return new PlayableMedia(new Metadata(stream.ToBytes(), vid.Title.ReplaceIllegalCharacters(), audioStreams[0].Container.ToString().ToLower(), vid.Duration));
        }

        private Task<MediaCollection> CacheAsync(MediaCollection col, MediaCache cache, bool pruneCache = true)
        {
            return cache.CacheMediaAsync(col, pruneCache || !col.IsPlaylist);
        }        

        public async Task<MediaCollection> DownloadToCacheAsync(MediaCache cache, QueueMode mode, Discord.IGuild guild, string searchQuery, bool pruneCache = true, Action largeSizeWarningCallback = null, Action<string> videoUnavailableCallback = null)
        {
            // Refactor this.
            if (YoutubeClient.TryParsePlaylistId(searchQuery, out var playlistId))
            {
                var pl = await yt.GetPlaylistAsync(playlistId, 1);
                var videos = pl.Videos;

                var plIndex = await Utility.Utility.GetURLArgumentValueAsync<int>(searchQuery, "index");

                if (videos.Sum(x => x.Duration.Minutes) > LargeSizeDurationMinuteThreshold)
                    largeSizeWarningCallback?.Invoke();

                PlayableMedia[] dlResult = new PlayableMedia[videos.Count];
                switch (mode)
                {
                    case QueueMode.Consistent:
                        for (int i = 0; i < videos.Count; i++)
                        {
                            var result = await InternalDownloadAsync(videos[i].Id, true, null, videoUnavailableCallback);
                            if (result == null)
                                continue;
                            dlResult[i] = result;
                        }
                        break;

                    case QueueMode.Fast:
                        Parallel.For(0, videos.Count, new ParallelOptions() { MaxDegreeOfParallelism = -1 }, i =>
                        {
                            var result = InternalDownloadAsync(videos[i].Id, true, null, videoUnavailableCallback).Result;
                            if (result == null)
                                return;
                            dlResult[i] = result;
                        });
                        break;
                }
                await CacheAsync(new MediaCollection(dlResult, pl.Title, plIndex), cache, pruneCache);

                //TODO: This could all probably be done more efficient.
                var filterList = dlResult.ToList();
                filterList.RemoveAll(x => x == null);
                dlResult = filterList.ToArray();

                return new MediaCollection(dlResult, pl.Title, (await Utility.Utility.GetURLArgumentValueAsync<int?>(searchQuery, "index", false)) ?? 1);
            }

            var media = await InternalDownloadAsync(searchQuery, false, largeSizeWarningCallback, videoUnavailableCallback);

            return await CacheAsync(media, cache);
        }       
    }
}