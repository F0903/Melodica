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
using YoutubeExplode.Videos.Streams;

namespace Suits.Jukebox.Services.Downloaders
{
    public class AsyncYoutubeDownloader : IAsyncDownloader
    {
        private static readonly YoutubeClient yt = new YoutubeClient(new System.Net.Http.HttpClient());

        public const int MaxCacheAttempts = 5;

        public const int MaxDownloadAttempts = 10;

        public const int MaxPlaylistVideos = 999;

        public Action<string>? VideoUnavailableCallback { get; set; }

        public async Task<MediaType> EvaluateMediaTypeAsync(string input)
        {
            if (!input.IsUrl())
                return MediaType.Video;

            try
            {
                await yt.Playlists.GetAsync(input);
                return MediaType.Playlist;
            }
            catch { }

            try
            {
                await yt.Videos.Streams.GetHttpLiveStreamUrlAsync(input);
                return MediaType.Livestream;
            }
            catch { }

            try
            {
                await yt.Videos.GetAsync(input);
                return MediaType.Video;
            }
            catch { }

            throw new Exception("Url is invalid.");
        }

        public async Task<bool> VerifyURLAsync(string url)
        {
            bool result;
            try
            {
                await EvaluateMediaTypeAsync(url);
                result = true;
            }
            catch { result = false; }
            return result;
        }

        public async Task<Metadata> GetMediaInfoAsync(string query)
        {
            YoutubeExplode.Playlists.Playlist? playlist = null;
            try
            {
                playlist = await yt.Playlists.GetAsync(query);
            }
            catch
            { }

            if (playlist != null)
            {
                var playlistVideos = yt.Playlists.GetVideosAsync(playlist.Id);
                if (playlist != null)
                {
                    return new Metadata() { Duration = await playlist.GetTotalDurationAsync(yt), ID = playlist.Id.Value, Thumbnail = playlistVideos.BufferAsync(1).Result.First().Thumbnails.MediumResUrl, Title = playlist.Title, URL = playlist.Url };
                }
            }
            else
            {
                YoutubeExplode.Videos.Video? video = null;
                try
                {
                    video = await yt.Videos.GetAsync(query);
                }
                catch
                { }

                if (video == null)
                    video = yt.Search.GetVideosAsync(query).BufferAsync(1).Result.First();
                return new Metadata() { Duration = video.Duration, ID = video.Id, Thumbnail = video.Thumbnails.MediumResUrl, Title = video.Title, URL = video.Url };
            }
            throw new Exception("Media could not be resolved to neither video nor playlist.");
        }

        public async Task<(Metadata playlist, IEnumerable<Metadata> videos)> DownloadPlaylistInfoAsync(string url)
        {
            var playlist = await yt.Playlists.GetAsync(url);
            var videos = yt.Playlists.GetVideosAsync(playlist.Id);

            List<Metadata> videoInfo = new List<Metadata>();
            await foreach (var item in videos)
                videoInfo.Add(new Metadata() { Duration = item.Duration, ID = item.Id, Thumbnail = item.Thumbnails.MediumResUrl, Title = item.Title, URL = item.Url });
            return (new Metadata() { Duration = await playlist.GetTotalDurationAsync(), ID = playlist.Id, Thumbnail = videoInfo.First().Thumbnail, Title = playlist.Title, URL = playlist.Url }, videoInfo);
        }

        public async Task<PlayableMedia> DownloadVideo(string query, bool isPreFiltered, int attempt = 0)
        {
            var video = isPreFiltered ? await yt.Videos.GetAsync(query) : yt.Search.GetVideosAsync(query).BufferAsync(MaxDownloadAttempts).Result.ElementAt(attempt);

            var manifest = await yt.Videos.Streams.GetManifestAsync(video.Id);

            async Task Error()
            {
                if (isPreFiltered || attempt > MaxDownloadAttempts)
                    throw new Exception($"{video.Title} could not be downloaded. {(attempt != 0 ? $"(Attempt {attempt} of {MaxDownloadAttempts})" : "")}");
                await DownloadVideo(query, isPreFiltered, attempt++).ConfigureAwait(false);
            }

            var streamInfo = manifest.GetAudioOnly().WithHighestBitrate();
            Stream? stream = null;
            try
            {
                stream = await yt.Videos.Streams.GetAsync(streamInfo ?? throw new Exception("No stream was found."));
            }
            catch
            {
                await Error().ConfigureAwait(false);
            }
            if (stream == null)
                await Error().ConfigureAwait(false);

            return new PlayableMedia(new Metadata() { Duration = video.Duration, ID = video.Id, Thumbnail = video.Thumbnails.MediumResUrl, Title = video.Title, URL = video.Url, Format = streamInfo!.Container.Name.ToLower() }, stream!.ToBytes());
        }

        public Task<MediaCollection> DownloadAsync(string query)
        {
            bool preFiltered;
            try
            {
                yt.Videos.GetAsync(query);
                preFiltered = true;
            }
            catch
            { preFiltered = false; }

            return Task.FromResult((MediaCollection)DownloadVideo(query, preFiltered).Result);
        }

        public Task<string> GetLivestreamAsync(string streamURL)
        {
            return yt.Videos.Streams.GetHttpLiveStreamUrlAsync(streamURL);
        }
    }
}