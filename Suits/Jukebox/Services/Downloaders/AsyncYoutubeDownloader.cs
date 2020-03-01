using Suits.Jukebox.Models;
using Suits.Jukebox.Services.Cache;
using Suits.Utility.Extensions;
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
using Discord;

namespace Suits.Jukebox.Services.Downloaders
{
    public class AsyncYoutubeDownloader : IAsyncDownloadService
    {
        public AsyncYoutubeDownloader(Action? largeSizeWarningCallback, Action<string>? videoUnavailableCallback)
        {
            LargeSizeWarningCallback = largeSizeWarningCallback;
            VideoUnavailableCallback = videoUnavailableCallback;
        }

        private readonly YoutubeClient yt = new YoutubeClient(new System.Net.Http.HttpClient());

        public const int MaxCacheAttempts = 5;

        public const int MaxDownloadAttempts = 10;

        public const int MaxPlaylistVideos = 100;

        private readonly TimeSpan LargeSizeDurationThreshold = new TimeSpan(0, 45, 0);

        public Action? LargeSizeWarningCallback { get; set; }
        public Action<string>? VideoUnavailableCallback { get; set; }

        public Task<string> GetMediaTitleAsync(string query) =>
            Task.FromResult(yt.SearchVideosAsync(query, 1).Result[0].Title);
      
        private async Task<MediaCollection> DownloadPlaylist(string id, int index = 0)
        {
            var pl = await yt.GetPlaylistAsync(id, 1);
            var videos = pl.Videos;

            if (videos.Sum(x => x.Duration) > LargeSizeDurationThreshold)
                LargeSizeWarningCallback?.Invoke();           

            var num = videos.Count > MaxPlaylistVideos ? MaxPlaylistVideos : videos.Count;
            List<PlayableMedia> vids = new List<PlayableMedia>(pl.Videos.Count);
            for (int i = index; i < num; i++)
            {
                var vid = videos[i];
                Stream vs = new MemoryStream();
                var iSet = await yt.GetVideoMediaStreamInfosAsync(vid.Id);
                var audInfo = iSet.Audio.OrderByDescending(x => x.Bitrate).First();
                await yt.DownloadMediaStreamAsync(audInfo, vs);
                vids.Add(new PlayableMedia(new Metadata(vid.Title, audInfo.Container.ToString(), vid.Duration, vid.Thumbnails.HighResUrl), vs.ToBytes()));
            }
            return new MediaCollection(vids, pl.Title);
        }

        private async Task<PlayableMedia> DownloadVideo(string query, bool isPreFiltered, int attempt = 0)
        {
            bool isQueryUrl = query.IsUrl();
            
            var vid = isQueryUrl || isPreFiltered ? (await yt.GetVideoAsync(isPreFiltered ? query : YoutubeClient.ParseVideoId(query))) : (await yt.SearchVideosAsync(query, 1))[attempt];

            if (vid.Duration > LargeSizeDurationThreshold)
                LargeSizeWarningCallback?.Invoke();

            MediaStreamInfoSet info;
            try
            {
                info = await yt.GetVideoMediaStreamInfosAsync(vid.Id);
            }
            catch (Exception ex) when (ex is VideoUnplayableException || ex is VideoUnavailableException)
            {
                VideoUnavailableCallback?.Invoke(vid.Title);
                return null!;
            }

            var audioStreams = info.Audio.OrderByDescending(x => x.Bitrate).ToArray();
            if (audioStreams.Length == 0)
            {
                if (isQueryUrl)
                    throw new Exception("This video does not have have available media streams.");

                if (attempt > MaxDownloadAttempts)
                    throw new Exception($"No videos with available media streams could be found. Attempts: {attempt}/{MaxDownloadAttempts}");

                return await DownloadVideo(query, false, ++attempt).ConfigureAwait(false);
            }
            var stream = await yt.GetMediaStreamAsync(audioStreams[0]);

            return new PlayableMedia(new Metadata(vid.Title.ReplaceIllegalCharacters(), audioStreams[0].Container.ToString().ToLower(), vid.Duration, vid.Thumbnails.HighResUrl), stream.ToBytes());
        }

        public async Task<MediaCollection> DownloadAsync(string searchQuery)
        {
            if(YoutubeClient.TryParsePlaylistId(searchQuery, out var plId))
            {
                var plIndex = await Utility.General.GetURLArgumentIntAsync(searchQuery, "index", false) - 1 ?? 0;
                return await DownloadPlaylist(plId!, plIndex);
            }
            return await DownloadVideo(searchQuery, false);
        }
    }
}