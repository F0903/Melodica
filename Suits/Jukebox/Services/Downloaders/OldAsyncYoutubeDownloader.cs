using Suits.Jukebox.Models;
using Suits.Jukebox.Services.Cache;
using Suits.Utility.Extensions;
using Suits.Jukebox.Models.Exceptions;
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
    [Obsolete]
    public class OldAsyncYoutubeDownloader : IAsyncDownloader
    {
        private static readonly YoutubeClient yt = new YoutubeClient(new System.Net.Http.HttpClient());

        public const int MaxCacheAttempts = 5;

        public const int MaxDownloadAttempts = 10;

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

        public async Task<MediaMetadata> GetMediaInfoAsync(string query)
        {
            YoutubeExplode.Playlists.Playlist? playlist = null;
            try
            {
                playlist = await yt.Playlists.GetAsync(query);
            }
            catch
            { }

            if (playlist != null) // If the query returns a playlist
            {
                var playlistVideos = yt.Playlists.GetVideosAsync(playlist.Id);
                if (playlist != null)
                {
                    return new MediaMetadata() 
                    {
                        MediaOrigin = MediaOrigin.YouTube,
                        MediaType = MediaType.Playlist, 
                        Duration = await playlist.GetTotalDurationAsync(yt), 
                        ID = playlist.Id.Value, 
                        Thumbnail = playlistVideos.BufferAsync(1).Result.First().Thumbnails.MediumResUrl, 
                        Title = playlist.Title, 
                        URL = playlist.Url 
                    };
                }
            }
            else // Else it is a video
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

                bool isLivestream = video.Duration == TimeSpan.Zero;
                return new MediaMetadata() 
                {
                    MediaOrigin = MediaOrigin.YouTube,
                    MediaType = isLivestream ? MediaType.Livestream : MediaType.Video,
                    Duration = video.Duration, 
                    ID = video.Id, Thumbnail = video.Thumbnails.MediumResUrl, 
                    Title = video.Title, 
                    URL = video.Url 
                };
            }
            throw new Exception("Media could not be resolved to neither video nor playlist.");
        }

        public async Task<(MediaMetadata playlist, IEnumerable<MediaMetadata> videos)> DownloadPlaylistInfoAsync(string url)
        {
            var playlist = await yt.Playlists.GetAsync(url);
            var videos = yt.Playlists.GetVideosAsync(playlist.Id);

            List<MediaMetadata> videoInfo = new List<MediaMetadata>();
            await foreach (var item in videos)
                videoInfo.Add(new MediaMetadata() 
                {
                    MediaOrigin = MediaOrigin.YouTube,
                    MediaType = MediaType.Video,
                    Duration = item.Duration, 
                    ID = item.Id, 
                    Thumbnail = item.Thumbnails.MediumResUrl, 
                    Title = item.Title, 
                    URL = item.Url 
                });

            return (new MediaMetadata() 
            {
                MediaOrigin = MediaOrigin.YouTube,
                MediaType = MediaType.Playlist, 
                Duration = await playlist.GetTotalDurationAsync(), 
                ID = playlist.Id, 
                Thumbnail = videoInfo.First().Thumbnail, 
                Title = playlist.Title, 
                URL = playlist.Url 
            }, videoInfo);
        }

        public async Task<PlayableMedia> DownloadVideo(string query, bool isPreFiltered, int attempt = 0)
        {
            var video = isPreFiltered ? await yt.Videos.GetAsync(query) : yt.Search.GetVideosAsync(query).BufferAsync(MaxDownloadAttempts).Result.ElementAt(attempt);

            StreamManifest manifest;
            try
            {
                manifest = await yt.Videos.Streams.GetManifestAsync(video.Id);
            }
            catch (Exception ex) when(ex is YoutubeExplode.Exceptions.VideoUnplayableException || 
                                      ex is YoutubeExplode.Exceptions.VideoUnavailableException ||
                                      ex is YoutubeExplode.Exceptions.VideoRequiresPurchaseException)
            {
                throw new MediaUnavailableException();
            }

            async Task Error()
            {
                if (isPreFiltered || attempt > MaxDownloadAttempts)
                    throw new Exception($"{video.Title} could not be downloaded. {(attempt != 0 ? $"(Attempt {attempt} of {MaxDownloadAttempts})" : "")}");
                await DownloadVideo(query, isPreFiltered, attempt++).ConfigureAwait(false);
            }

            var streamInfo = manifest!.GetAudioOnly().WithHighestBitrate();
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

            GC.Collect();
            var meta = new MediaMetadata()
            {
                MediaOrigin = MediaOrigin.YouTube,
                MediaType = MediaType.Video,
                Duration = video.Duration,
                ID = video.Id,
                Thumbnail = video.Thumbnails.MediumResUrl,
                Title = video.Title,
                URL = video.Url
            };
            meta.DataInformation.Format = streamInfo!.Container.Name.ToLower();
            return new PlayableMedia(meta, stream!);
        }

        public Task<PlayableMedia> DownloadAsync(string query)
        {
            bool preFiltered;
            try
            {
                yt.Videos.GetAsync(query);
                preFiltered = true;
            }
            catch
            { preFiltered = false; }

            return Task.FromResult(DownloadVideo(query, preFiltered).Result);
        }

        public Task<string> GetLivestreamAsync(string streamURL)
        {
            return yt.Videos.Streams.GetHttpLiveStreamUrlAsync(streamURL);
        }
    }
}