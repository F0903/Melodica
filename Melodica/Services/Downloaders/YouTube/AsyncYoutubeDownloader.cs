using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Melodica.Services.Caching;
using Melodica.Services.Downloaders.Exceptions;
using Melodica.Services.Media;
using Melodica.Utility.Extensions;

using YoutubeExplode;
using YoutubeExplode.Playlists;
using YoutubeExplode.Videos;
using YoutubeExplode.Videos.Streams;

namespace Melodica.Services.Downloaders.YouTube
{
    //TODO: Rewrite
    public class AsyncYoutubeDownloader : IAsyncDownloader
    {
        private readonly YoutubeClient yt = new YoutubeClient();
        private readonly MediaFileCache cache = new MediaFileCache("YouTube");

        public bool IsUrlSupported(string url) => url.StartsWith("https://www.youtube.com/") || url.StartsWith("http://www.youtube.com/");

        private async Task<PlayableMedia> DownloadVideo(MediaInfo meta)
        {
            var vidStreams = await yt.Videos.Streams.GetManifestAsync(meta.Id ?? throw new DownloaderException("ID was null in DownloadVideo."));
            var vidAudioStream = vidStreams.GetAudioOnly().WithHighestBitrate() ?? throw new DownloaderException("Could not get audio stream from video.");

            var rawStream = await yt.Videos.Streams.GetAsync(vidAudioStream!);

            meta.DataInformation.Format = vidAudioStream!.Container.Name.ToLower();
            return await cache.CacheMediaAsync(new PlayableMedia(meta, rawStream!));
        }

        private static bool IsUnavailable(Exception ex) =>
            ex is YoutubeExplode.Exceptions.VideoUnplayableException ||
            ex is YoutubeExplode.Exceptions.VideoUnavailableException ||
            ex is YoutubeExplode.Exceptions.VideoRequiresPurchaseException;

        private static Task<string> ParseURLToIdAsync(ReadOnlySpan<char> url)
        {
            if (!(url.StartsWith("https://") || url.StartsWith("http://")))
                return Task.FromResult(url.ToString()); // Just return, cause the url is probably already an id.
            int startIndex = url.IndexOf("?v=") + 3;
            int stopIndex = url.Contains('&') ? url.IndexOf('&') : url.Length;
            string? id = url[startIndex..stopIndex].ToString();
            return Task.FromResult(id);
        }

        private async Task<MediaInfo> SearchOrGetVideo(string input)
        {
            async Task<MediaInfo> SearchVideo(int attempt = 0)
            {
                if (attempt > 3) throw new MediaUnavailableException();

                try
                {
                    var videos = yt.Search.GetVideosAsync(input);
                    var bufVideos = await videos.BufferAsync(attempt + 1);
                    var ytVideo = bufVideos.ElementAtOrDefault(attempt);
                    if (ytVideo is null)
                        return await SearchVideo(++attempt).ConfigureAwait(false);

                    return VideoToMetadata(ytVideo);
                }
                catch (Exception ex) when (IsUnavailable(ex))
                {
                    throw new MediaUnavailableException();
                }
            }

            if (input.IsUrl())
            {
                var video = await yt.Videos.GetAsync(input);
                return VideoToMetadata(video);
            }
            return await SearchVideo().ConfigureAwait(false);
        }

        public async Task<PlayableMedia> DownloadAsync(MediaInfo meta)
        {
            if (meta.Id == null) throw new DownloaderException("Id of media was null. Unable to download.");
            if (cache.Contains(meta.Id))
                return await cache.GetAsync(meta.Id);

            try
            {
                if (meta!.MediaType == MediaType.Playlist)
                    throw new NotSupportedException(); // Something went very wrong.

                if (meta.MediaType == MediaType.Livestream)
                    return new PlayableMedia(meta, null); // Something went very wrong.

                return await DownloadVideo(meta);
            }
            catch (Exception ex)
            {
                if (IsUnavailable(ex))
                    throw new MediaUnavailableException();
                else
                    throw new DownloaderException("Critical error happened in YT DownloadAsync.", ex);
            }
        }

        public async Task<PlayableMedia> DownloadAsync(string query)
        {
            MediaInfo? meta = await GetMediaInfoAsync(query);
            return await DownloadAsync(meta);
        }

        public async Task<(MediaInfo playlist, IEnumerable<MediaInfo> videos)> DownloadPlaylistInfoAsync(string url)
        {
            var pl = await yt.Playlists.GetAsync(url);
            var plMeta = await PlaylistToMetadata(pl);

            var plVideos = yt.Playlists.GetVideosAsync(pl.Id);
            var plVideoMeta = new List<MediaInfo>(10);
            int i = 0;
            await foreach (var video in plVideos)
            {
                plVideoMeta.Add(VideoToMetadata(video));
                i++;
            }
            return (plMeta, plVideoMeta);
        }

        public Task<string> GetLivestreamAsync(string streamURL) => yt.Videos.Streams.GetHttpLiveStreamUrlAsync(streamURL);

        static MediaInfo VideoToMetadata(Video video)
        {
            var (artist, title) = video.Title.AsSpan().SeperateArtistName();
            return new YoutubeMediaInfo()
            {
                Title = title,
                Artist = artist,
                Duration = video.Duration,
                Id = video.Id,
                Url = video.Url,
                ImageUrl = video.Thumbnails.MediumResUrl,
                MediaType = MediaType.Video
            };
        }

        static MediaInfo VideoToMetadata(PlaylistVideo video)
        {
            var (artist, title) = video.Title.AsSpan().SeperateArtistName();
            return new YoutubeMediaInfo()
            {
                Title = title,
                Artist = artist,
                Duration = video.Duration,
                Id = video.Id,
                Url = video.Url,
                ImageUrl = video.Thumbnails.MediumResUrl,
                MediaType = MediaType.Video
            };
        }


        private Task<MediaInfo> PlaylistToMetadata(Playlist pl)
        {
            var (artist, newTitle) = pl.Title.AsSpan().SeperateArtistName();
            return Task.FromResult(
                (MediaInfo)new YoutubeMediaInfo()
            {
                MediaType = MediaType.Playlist,
                Duration = pl.GetTotalDurationAsync(yt).GetAwaiter().GetResult(),
                Id = pl.Id,
                ImageUrl = pl.GetPlaylistThumbnail(yt).GetAwaiter().GetResult(),
                Title = newTitle,
                Artist = artist,
                Url = pl.Url
            });
        }

        public bool IsUrlPlaylistAsync(string url) => url.StartsWith(@"http://www.youtube.com/playlist?list=") || url.StartsWith(@"https://www.youtube.com/playlist?list=");

        protected Task<MediaType> EvaluateMediaTypeAsync(string input)
        {
            // if it is id, count the letters to determine
            if (!input.IsUrl())
            {
                if (input.AsSpan().LikeYouTubeId()) // If input looks like an ID
                {
                    return Task.FromResult(input.Length == 11 ?
                        MediaType.Video :
                        input.Length == 34 ?
                        MediaType.Playlist :
                        MediaType.Video); // If input mismatched, just default to a video.
                }
                else return Task.FromResult(MediaType.Video);
            }

            if (input.StartsWith(@"http://www.youtube.com/watch?v=") || input.StartsWith(@"https://www.youtube.com/watch?v="))
            {
                try
                {
                    var vid = yt.Videos.GetAsync(input).Result;
                    bool isLive = vid.Duration == TimeSpan.Zero;
                    return Task.FromResult(isLive ? MediaType.Livestream : MediaType.Video);
                }
                catch (Exception) { throw new DownloaderException("Could not get video. ID might not be valid.\nIf this issue persists, please try again later or contact the developer."); }
            }

            if (IsUrlPlaylistAsync(input))
                return Task.FromResult(MediaType.Playlist);

            throw new DownloaderException("MediaType could not be evaluated. (YT)");
        }

        public Task<MediaInfo> GetMediaInfoAsync(string input)
        {
            MediaInfo GetLivestream()
            {
                var meta = SearchOrGetVideo(input).Result;
                meta.DataInformation.Format = "hls";
                meta.DataInformation.MediaPath = yt.Videos.Streams.GetHttpLiveStreamUrlAsync(meta.Id ?? throw new NullReferenceException("Video ID was null.")).Result;
                return meta;
            }

            if (input.IsUrl())
            {
                var id = ParseURLToIdAsync(input).Result;
                if (cache.Contains(id))
                    return Task.FromResult(cache.GetAsync(id).Result.Info);
            }

            if (input.AsSpan().LikeYouTubeId())
            {
                if (cache.Contains(input))
                    return Task.FromResult(cache.GetAsync(input).Result.Info);
            }

            var mType = EvaluateMediaTypeAsync(input).Result;

            // USE INTERFACES OR SOMETHING ELSE (VERY BAD)
            object mediaObj = mType switch
            {
                MediaType.Video => SearchOrGetVideo(input).Result,
                MediaType.Playlist => yt.Playlists.GetAsync(input).Result,
                MediaType.Livestream => GetLivestream(),
                _ => throw new NotImplementedException(),
            };

            return mType switch
            {
                MediaType.Video => Task.FromResult((MediaInfo)mediaObj),
                MediaType.Playlist => PlaylistToMetadata((Playlist)mediaObj),
                MediaType.Livestream => Task.FromResult((MediaInfo)mediaObj),
                _ => throw new NotImplementedException(),
            };
        }

        public async Task<bool> VerifyUrlAsync(string url)
        {
            try
            {
                await EvaluateMediaTypeAsync(url);
                return true;
            }
            catch (Exception)
            { return false; }
        }

        Task<MediaType> IAsyncDownloader.EvaluateMediaTypeAsync(string url) => throw new NotSupportedException();
    }
}