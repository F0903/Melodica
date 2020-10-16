using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Melodica.Services.Downloaders.Exceptions;
using Melodica.Services.Models;
using Melodica.Services.Services;
using Melodica.Utility;
using Melodica.Utility.Extensions;

using YoutubeExplode;
using YoutubeExplode.Playlists;
using YoutubeExplode.Videos;
using YoutubeExplode.Videos.Streams;

namespace Melodica.Services.Downloaders.YouTube
{
    public class AsyncYoutubeDownloader : AsyncDownloaderBase
    {
        private readonly YoutubeClient yt = new YoutubeClient();
        private readonly MediaFileCache cache = new MediaFileCache("YouTube");

        public override bool IsUrlSupported(string url) => url.StartsWith("https://www.youtube.com/") || url.StartsWith("http://www.youtube.com/");

        private async Task<PlayableMedia> DownloadVideo(MediaMetadata meta)
        {
            var vidStreams = await yt.Videos.Streams.GetManifestAsync(meta.Id ?? throw new DownloaderException("ID was null in DownloadVideo."));
            var vidAudioStream = vidStreams.GetAudioOnly().WithHighestBitrate() ?? throw new DownloaderException("Could not get audio stream from video.");

            var rawStream = await yt.Videos.Streams.GetAsync(vidAudioStream!);

            meta.DataInformation.Format = vidAudioStream!.Container.Name.ToLower();
            return await cache.CacheMediaAsync(new PlayableMedia(meta, rawStream!));
        }

        private bool IsUnavailable(Exception ex) =>
            ex is YoutubeExplode.Exceptions.VideoUnplayableException ||
            ex is YoutubeExplode.Exceptions.VideoUnavailableException ||
            ex is YoutubeExplode.Exceptions.VideoRequiresPurchaseException;

        private Task<string> ParseURLToIdAsync(ReadOnlySpan<char> url)
        {
            if (!(url.StartsWith("https://") || url.StartsWith("http://")))
                return Task.FromResult(url.ToString()); // Just return, cause the url is probably already an id.
            int startIndex = url.IndexOf("?v=") + 3;
            int stopIndex = url.Contains('&') ? url.IndexOf('&') : url.Length;
            string? id = url[startIndex..stopIndex].ToString();
            return Task.FromResult(id);
        }

        private Task<Video> SearchOrGetVideo(string input)
        {
            Video SearchVideo(int attempt = 0)
            {
                if (attempt > 3) throw new MediaUnavailableException();

                Video video;
                try
                {
                    var videos = yt.Search.GetVideosAsync(input);
                    var bufVideos = videos.BufferAsync(attempt + 1).Result;
                    video = bufVideos.ElementAtOrDefault(attempt);
                }
                catch (Exception ex) when (IsUnavailable(ex))
                {
                    throw new MediaUnavailableException();
                }

                return video ?? SearchVideo(++attempt);
            }
            return Task.FromResult(input.IsUrl() ? yt.Videos.GetAsync(input).Result : SearchVideo());
        }

        public override async Task<PlayableMedia> DownloadAsync(MediaMetadata meta)
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

        public override async Task<PlayableMedia> DownloadAsync(string query)
        {
            MediaMetadata? meta = await GetMediaInfoAsync(query);
            return await DownloadAsync(meta);
        }

        public override async Task<(MediaMetadata playlist, IEnumerable<MediaMetadata> videos)> DownloadPlaylistInfoAsync(string url)
        {
            var pl = await yt.Playlists.GetAsync(url);
            var plMeta = await GetPlaylistMetadataAsync(pl);

            var plVideos = yt.Playlists.GetVideosAsync(pl.Id);
            var plVideoMeta = new List<MediaMetadata>(10);
            int i = 0;
            await foreach (var video in plVideos)
            {
                plVideoMeta.Add(await GetVideoMetadataAsync(video));
                i++;
            }
            return (plMeta, plVideoMeta);
        }

        public override Task<string> GetLivestreamAsync(string streamURL) => yt.Videos.Streams.GetHttpLiveStreamUrlAsync(streamURL);

        private Task<MediaMetadata> GetVideoMetadataAsync(Video video)
        {
            var (artist, newTitle) = video.Title.AsSpan().SeperateArtistName();
            return Task.FromResult(new MediaMetadata()
            {
                Origin = MediaOrigin.YouTube,
                MediaType = MediaType.Video,
                Duration = video.Duration,
                Id = video.Id,
                Thumbnail = video.Thumbnails.MediumResUrl,
                Title = newTitle,
                Artist = artist,
                Url = video.Url
            });
        }

        private Task<MediaMetadata> GetPlaylistMetadataAsync(Playlist pl)
        {
            var (artist, newTitle) = pl.Title.AsSpan().SeperateArtistName();
            return Task.FromResult(new MediaMetadata()
            {
                Origin = MediaOrigin.YouTube,
                MediaType = MediaType.Playlist,
                Duration = pl.GetTotalDurationAsync(yt).GetAwaiter().GetResult(),
                Id = pl.Id,
                Thumbnail = pl.GetPlaylistThumbnail(yt).GetAwaiter().GetResult(),
                Title = newTitle,
                Artist = artist,
                Url = pl.Url
            });
        }

        public override bool IsUrlPlaylistAsync(string url) => url.StartsWith(@"http://www.youtube.com/playlist?list=") || url.StartsWith(@"https://www.youtube.com/playlist?list=");

        protected override Task<MediaType> EvaluateMediaTypeAsync(string input)
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

        public override Task<MediaMetadata> GetMediaInfoAsync(string input)
        {
            MediaMetadata GetLivestream()
            {
                var vidInfo = SearchOrGetVideo(input).Result;
                var (artist, newTitle) = vidInfo.Title.AsSpan().SeperateArtistName();
                var meta = new MediaMetadata()
                {
                    Duration = vidInfo.Duration,
                    Id = vidInfo.Id,
                    Origin = MediaOrigin.YouTube,
                    MediaType = MediaType.Livestream,
                    Thumbnail = vidInfo.Thumbnails.MediumResUrl,
                    Title = newTitle,
                    Artist = artist,
                    Url = vidInfo.Url
                };
                meta.DataInformation.Format = "hls";
                meta.DataInformation.MediaPath = yt.Videos.Streams.GetHttpLiveStreamUrlAsync(vidInfo.Id).Result;
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

            // This whole object casting thing smells a bit bad
            object mediaObj = mType switch
            {
                MediaType.Video => SearchOrGetVideo(input).Result,
                MediaType.Playlist => yt.Playlists.GetAsync(input).Result,
                MediaType.Livestream => GetLivestream(),
                _ => throw new NotImplementedException(),
            };

            return mType switch
            {
                MediaType.Video => GetVideoMetadataAsync((Video)mediaObj),
                MediaType.Playlist => GetPlaylistMetadataAsync((Playlist)mediaObj),
                MediaType.Livestream => Task.FromResult((MediaMetadata)mediaObj),
                _ => throw new NotImplementedException(),
            };
        }

        public override async Task<bool> VerifyUrlAsync(string url)
        {
            try
            {
                await EvaluateMediaTypeAsync(url);
                return true;
            }
            catch (Exception)
            { return false; }
        }
    }
}
