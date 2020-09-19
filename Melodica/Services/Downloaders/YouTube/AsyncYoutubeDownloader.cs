using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Melodica.Core.Exceptions;
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
        readonly YoutubeClient yt = new YoutubeClient();

        readonly MediaFileCache cache = new MediaFileCache("YouTube");

        public override bool IsUrlSupported(string url) => url.StartsWith("https://www.youtube.com/") || url.StartsWith("http://www.youtube.com/");

        private async Task<PlayableMedia> DownloadVideo(Video video, MediaMetadata meta)
        {
            var newMeta = new MediaMetadata()
            {
                ID = video.Id,
                Duration = meta.Duration,
                MediaOrigin = meta.MediaOrigin,
                MediaType = meta.MediaType,
                Thumbnail = meta.Thumbnail,
                Title = meta.Title,
                URL = meta.URL
            };

            if (cache.Contains(video.Id))
            {
                var cacheMed = await cache.GetAsync(video.Id);
                newMeta.DataInformation = cacheMed.Info.DataInformation;

                var newMed = new PlayableMedia(newMeta, null);
                return newMed;
            }
            var vidStreams = await yt.Videos.Streams.GetManifestAsync(video.Id);
            var vidAudioStream = vidStreams.GetAudioOnly().WithHighestBitrate();
            Assert.NotNull(vidAudioStream);

            var rawStream = await yt.Videos.Streams.GetAsync(vidAudioStream!);

            newMeta.DataInformation.Format = vidAudioStream!.Container.Name.ToLower();
            return await cache.CacheMediaAsync(new PlayableMedia(newMeta, rawStream!));
        }

        private bool IsUnavailable(Exception ex) =>
            ex is YoutubeExplode.Exceptions.VideoUnplayableException ||
            ex is YoutubeExplode.Exceptions.VideoUnavailableException ||
            ex is YoutubeExplode.Exceptions.VideoRequiresPurchaseException;

        private Task<string> ParseURLToIdAsync(string url)
        {
            if (!(url.StartsWith("https://") || url.StartsWith("http://")))
                return Task.FromResult(url); // Just return, cause the url is probably already an id.
            var startIndex = url.IndexOf("?v=") + 3;
            var stopIndex = url.Contains('&') ? url.IndexOf('&') : url.Length;
            var id = url[startIndex..stopIndex];
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

        public override async Task<PlayableMedia> DownloadAsync(string query)
        {
            string vidId;
            MediaMetadata? meta;
            if (query.IsUrl())
            {
                vidId = await ParseURLToIdAsync(query);
                meta = await GetMediaInfoAsync(query);
            }
            else
            {
                meta = await GetMediaInfoAsync(query);
                vidId = meta.ID!;
            }
 
            if (vidId == null) throw new CriticalException("Id of media was null. Unable to download.");
            if (cache.Contains(vidId))
                return await cache.GetAsync(vidId);

            try
            {
                if (meta!.MediaType == MediaType.Playlist)
                    throw new NotSupportedException();

                if (meta.MediaType == MediaType.Livestream)
                    return new PlayableMedia(meta, null);

                var vidInfo = await yt.Videos.GetAsync(vidId); ///
                return await DownloadVideo(vidInfo, meta);
            }
            catch (Exception ex)
            {
                if (IsUnavailable(ex))
                    throw new MediaUnavailableException();
                else
                    throw new CriticalException("Critical error happened in YT DownloadAsync.", ex);
            }
        }

        public override async Task<(MediaMetadata playlist, IEnumerable<MediaMetadata> videos)> DownloadPlaylistInfoAsync(string url)
        {
            var pl = await yt.Playlists.GetAsync(url);
            var plMeta = await GetPlaylistMetadataAsync(pl);

            var plVideos = yt.Playlists.GetVideosAsync(pl.Id);
            List<MediaMetadata> plVideoMeta = new List<MediaMetadata>(10);
            int i = 0;
            await foreach (var video in plVideos)
            {
                plVideoMeta.Add(await GetVideoMetadataAsync(video));
                i++;
            }
            return (plMeta, plVideoMeta);
        }

        public override Task<string> GetLivestreamAsync(string streamURL)
        {
            return yt.Videos.Streams.GetHttpLiveStreamUrlAsync(streamURL);
        }

        private Task<MediaMetadata> GetVideoMetadataAsync(Video video)
        {
            return Task.FromResult(new MediaMetadata()
            {
                MediaOrigin = MediaOrigin.YouTube,
                MediaType = MediaType.Video,
                Duration = video.Duration,
                ID = video.Id,
                Thumbnail = video.Thumbnails.MediumResUrl,
                Title = video.Title,
                URL = video.Url
            });
        }

        private Task<MediaMetadata> GetPlaylistMetadataAsync(Playlist pl)
        {
            return Task.FromResult(new MediaMetadata()
            {
                MediaOrigin = MediaOrigin.YouTube,
                MediaType = MediaType.Playlist,
                Duration = pl.GetTotalDurationAsync(yt).GetAwaiter().GetResult(),
                ID = pl.Id,
                Thumbnail = pl.GetPlaylistThumbnail(yt).GetAwaiter().GetResult(),
                Title = pl.Title,
                URL = pl.Url
            });
        }

        public override bool IsPlaylistAsync(string url) => url.StartsWith(@"http://www.youtube.com/playlist?list=") || url.StartsWith(@"https://www.youtube.com/playlist?list=");

        protected override Task<MediaType> EvaluateMediaTypeAsync(string input)
        {
            // if it is id, count the letters to determine
            if (!input.IsUrl())
            {
                if (input.LikeYouTubeId()) // If input looks like an ID
                    return Task.FromResult(input.Length == 11 ?
                        MediaType.Video :
                        input.Length == 34 ?
                        MediaType.Playlist :
                        MediaType.Video); // If input was mistaken for an ID.
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
                catch (Exception) { throw new CriticalException("ID might not be valid."); }
            }

            if (IsPlaylistAsync(input))
                return Task.FromResult(MediaType.Playlist);

            throw new CriticalException("MediaType could not be evaluated. (YT)");
        }

        public override Task<MediaMetadata> GetMediaInfoAsync(string input)
        {          
            MediaMetadata GetLivestream()
            {
                var vidInfo = SearchOrGetVideo(input).Result;
                var meta = new MediaMetadata()
                {
                    Duration = vidInfo.Duration,
                    ID = vidInfo.Id,
                    MediaOrigin = MediaOrigin.YouTube,
                    MediaType = MediaType.Livestream,
                    Thumbnail = vidInfo.Thumbnails.MediumResUrl,
                    Title = vidInfo.Title,
                    URL = vidInfo.Url
                };
                meta.DataInformation.Format = "hls";
                meta.DataInformation.MediaPath = yt.Videos.Streams.GetHttpLiveStreamUrlAsync(vidInfo.Id).Result;
                return meta;
            }

            var mType = EvaluateMediaTypeAsync(input).Result;

            // This whole object casting thing is smells bad
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

        public override async Task<bool> VerifyURLAsync(string url)
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
