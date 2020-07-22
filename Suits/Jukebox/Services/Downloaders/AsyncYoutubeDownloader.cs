using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AngleSharp.Common;
using Suits.Jukebox.Models;
using Suits.Jukebox.Models.Exceptions;
using Suits.Jukebox.Services;
using Suits.Utility;
using Suits.Utility.Extensions;
using YoutubeExplode;
using YoutubeExplode.Playlists;
using YoutubeExplode.Videos;
using YoutubeExplode.Videos.Streams;

namespace Suits.Jukebox.Services.Downloaders
{
    public class AsyncYoutubeDownloader : IAsyncDownloader
    {
        readonly YoutubeClient yt = new YoutubeClient();

        readonly MediaCache cache = new MediaCache("YouTube");

        private async Task<PlayableMedia> DownloadVideo(Video video)
        {
            var vidStreams = await yt.Videos.Streams.GetManifestAsync(video.Id);
            var vidAudioStream = vidStreams.GetAudioOnly().WithHighestBitrate();
            Assert.NotNull(vidAudioStream);

            var rawStream = await yt.Videos.Streams.GetAsync(vidAudioStream!);

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
            meta.DataInformation.Format = vidAudioStream!.Container.Name.ToLower();
            return new PlayableMedia(meta, rawStream!);
        }

        private bool IsUnavailable(Exception ex) =>
            ex is YoutubeExplode.Exceptions.VideoUnplayableException ||
            ex is YoutubeExplode.Exceptions.VideoUnavailableException ||
            ex is YoutubeExplode.Exceptions.VideoRequiresPurchaseException;

        private Task<string> SearchOrGetVideo(string input)
        {
            string GetVideo(int attempt = 0)
            {
                if (attempt > 3) throw new MediaUnavailableException();

                string? video;
                try
                {
                    var videos = yt.Search.GetVideosAsync(input);
                    var bufVideos = videos.BufferAsync(attempt).Result;
                    video = bufVideos.ElementAtOrDefault(attempt)?.Url;
                }
                catch (Exception ex) when (IsUnavailable(ex))
                {
                    throw new MediaUnavailableException();
                }

                return video ?? GetVideo(++attempt);
            }
            return Task.FromResult(input.IsUrl() ? input : GetVideo());
        }

        // Expects that GetMediaInfoAsync has been called prior. Thus the parameter.
        public Task<PlayableMedia> DownloadAsync(MediaMetadata meta)
        {
            bool inCache = cache.Contains(meta.ID!);
            try
            {
                return inCache ? cache.GetAsync(meta.ID!) : meta.MediaType switch
                {
                    MediaType.Video => Task.FromResult(cache.CacheMediaAsync(DownloadVideo(yt.Videos.GetAsync(meta.ID!).Result).Result).Result as PlayableMedia),
                    MediaType.Playlist => throw new NotSupportedException(),
                    MediaType.Livestream => Task.FromResult(new PlayableMedia(meta, null)),
                    _ => throw new NotSupportedException(),
                };
            }
            catch (Exception ex)
            {
                if (IsUnavailable(ex))
                    throw new MediaUnavailableException();
                else
                    throw new CriticalException(null, ex);
            }
        }

        public async Task<(MediaMetadata playlist, IEnumerable<MediaMetadata> videos)> DownloadPlaylistInfoAsync(string url)
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

        public bool IsUrlSupported(string url)
        {
            if (url.Contains("https://www.youtube.com/"))
                return true;
            else return false;
        }

        public Task<string> GetLivestreamAsync(string streamURL)
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

        private Task<MediaType> EvaluateMediaTypeAsync(string input)
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

            if (input.Contains(@"https://www.youtube.com/watch?v="))
            {
                try
                {
                    var vid = yt.Videos.GetAsync(input).Result;
                    bool isLive = vid.Duration == TimeSpan.Zero;
                    return Task.FromResult(isLive ? MediaType.Livestream : MediaType.Video);
                }
                catch (Exception) { throw new CriticalException("ID might not be valid."); }
            }

            if (input.Contains(@"https://www.youtube.com/playlist?list="))
                return Task.FromResult(MediaType.Playlist);

            throw new CriticalException("MediaType could not be evaluated. (YT)");
        }

        public Task<MediaMetadata> GetMediaInfoAsync(string input)
        {
            input = SearchOrGetVideo(input).Result;
            var mType = EvaluateMediaTypeAsync(input).Result;

            MediaMetadata GetLivestream()
            {
                var vidInfo = yt.Videos.GetAsync(input).Result;

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

            object mediaObj = mType switch
            {
                MediaType.Video => yt.Videos.GetAsync(input).Result,
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

        public async Task<bool> VerifyURLAsync(string url)
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
