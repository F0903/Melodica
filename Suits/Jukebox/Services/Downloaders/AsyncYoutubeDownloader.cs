using Suits.Jukebox.Models;
using Suits.Jukebox.Services.Cache;
using Suits.Utility.Extensions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using YoutubeExplode;
using YoutubeExplode.Exceptions;
using YoutubeExplode.Models.MediaStreams;

namespace Suits.Jukebox.Services.Downloaders
{
    public class AsyncYoutubeDownloader : IAsyncDownloader
    {
        private readonly YoutubeClient yt = new YoutubeClient(new System.Net.Http.HttpClient());

        public const int MaxCacheAttempts = 5;

        public const int MaxDownloadAttempts = 10;

        public const int MaxPlaylistVideos = 100;

        public Action<string>? VideoUnavailableCallback { get; set; }

        public Task<bool> IsPlaylistAsync(string url) => Task.FromResult(YoutubeClient.ValidatePlaylistId(YoutubeClient.TryParsePlaylistId(url, out var pId) != false ? pId! : ""));

        public Task<string> GetMediaTitleAsync(string query) =>
            Task.FromResult(yt.SearchVideosAsync(query, 1).Result[0].Title);

        public Task<bool> VerifyURLAsync(string url)
        {
            bool isPlaylist = YoutubeClient.TryParsePlaylistId(url, out var plID);
            bool couldValidate = isPlaylist ? YoutubeClient.ValidatePlaylistId(plID!) : YoutubeClient.ValidateVideoId(url);
            bool couldParse = couldValidate ? true : YoutubeClient.TryParseVideoId(url, out var _);
            return Task.FromResult(couldValidate || couldParse);
        }

        public async Task<IMediaInfo> GetMediaInfoAsync(string query)
        {
            bool isUrl = query.IsUrl();
            string? playlistID = null;
            bool isPlaylist = isUrl ? YoutubeClient.ValidatePlaylistId(YoutubeClient.TryParsePlaylistId(query, out playlistID) ? playlistID! : "") : YoutubeClient.ValidatePlaylistId(query);
            if (isPlaylist)
            {
                var playlist = await yt.GetPlaylistAsync(playlistID!, 1);
                return new MediaInfo() { Duration = playlist.GetTotalDuration(), Thumbnail = playlist.Videos.First().Thumbnails.MediumResUrl, Title = playlist.Title };
            }
            else
            {
                var queryIsID = YoutubeClient.ValidateVideoId(query);
                var couldParse = YoutubeClient.TryParseVideoId(query, out var videoID);
                var video = queryIsID || couldParse ? await yt.GetVideoAsync(queryIsID ? query : videoID!) : (await yt.SearchVideosAsync(query, 1)).First();
                return new MediaInfo() { Duration = video.Duration, Thumbnail = video.Thumbnails.MediumResUrl, Title = video.Title };
            }
        }

        public async Task<IEnumerable<string>> GetPlaylistVideoURLsAsync(string url)
        {
            var id = YoutubeClient.ParsePlaylistId(url);
            var pl = await yt.GetPlaylistAsync(id, 1);
            return pl.Videos.Convert(x => x.Id);
        }

        public async Task<PlayableMedia> DownloadVideo(string query, bool isPreFiltered, int attempt = 0)
        {
            bool isQueryUrl = query.IsUrl();

            var vid = isQueryUrl || isPreFiltered ? (await yt.GetVideoAsync(isPreFiltered ? query : YoutubeClient.ParseVideoId(query))) : (await yt.SearchVideosAsync(query, 1))[attempt];
            var normVidTitle = vid.Title.ReplaceIllegalCharacters();

            if (MediaCache.Contains(normVidTitle))
            {
                return await MediaCache.GetAsync(normVidTitle);
            }

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

            try
            {
                var _ = stream.ReadByte();
                stream.Seek(0, SeekOrigin.Begin);
            }
            catch (System.Net.Http.HttpRequestException)
            {
                if (isQueryUrl)
                    throw new Exception("Specified video is unavailable.");
                VideoUnavailableCallback?.Invoke(vid.Title);
                return await DownloadVideo(query, false, ++attempt).ConfigureAwait(false);
            }

            return new PlayableMedia(new Metadata(normVidTitle, audioStreams[0].Container.ToString().ToLower(), vid.Duration, vid.Thumbnails.HighResUrl), stream.ToBytes());
        }

        public Task<MediaCollection> DownloadAsync(string query)
        {
            bool preFiltered = YoutubeClient.ValidateVideoId(query);

            return Task.FromResult((MediaCollection)DownloadVideo(query, preFiltered).Result);
        }
    }
}