using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Suits.Jukebox.Models;
using Suits.Jukebox.Models.Exceptions;
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

        private async Task<PlayableMedia> DownloadVideo(Video video)
        {
            var vidStreams = await yt.Videos.Streams.GetManifestAsync(video.Id);
            var vidAudioStream = vidStreams.GetAudioOnly().WithHighestBitrate();
            Assert.NotNull(vidAudioStream);

            var rawStream = await yt.Videos.Streams.GetAsync(vidAudioStream!);

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
            meta.DataInformation.Format = vidAudioStream!.Container.Name.ToLower();
            return new PlayableMedia(meta, rawStream!);
        }

        private bool IsUnavailable(Exception ex) =>
            ex is YoutubeExplode.Exceptions.VideoUnplayableException ||
            ex is YoutubeExplode.Exceptions.VideoUnavailableException ||
            ex is YoutubeExplode.Exceptions.VideoRequiresPurchaseException;

        private Task<string> SearchOrGetVideo(string input, int attempt = 0)
        {
            try
            {
                return Task.FromResult(input.IsUrl() ? input : yt.Search.GetVideosAsync(input).BufferAsync(1).Result.ElementAt(attempt).Url);
            }
            catch (Exception ex) when (IsUnavailable(ex))
            {
                if (attempt > 4) throw new MediaUnavailableException();
                return SearchOrGetVideo(input, ++attempt);
            }
        }

        public Task<PlayableMedia> DownloadAsync(string query)
        {
            var url = SearchOrGetVideo(query).Result;
            var (mType, typeObj) = InternalEvaluateMediaType(url).Result;

            try
            {
                return mType switch
                {
                    MediaType.Video => DownloadVideo((Video)typeObj),
                    MediaType.Playlist => throw new NotSupportedException(),
                    MediaType.Livestream => Task.FromResult(new PlayableMedia((MediaMetadata)typeObj, null)),
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

        private async Task<(MediaType type, object obj)> InternalEvaluateMediaType(string url)
        {
            // if it is id, count the letters to determine
            if (!url.IsUrl())
                throw new CriticalException("Non url got passed into a url accepting function. Something went very wrong here... (int)");

            if (url.Contains(@"https://www.youtube.com/watch?v="))
            {
                try
                {
                    var vid = await yt.Videos.GetAsync(url);
                    bool isLive = vid.Duration == TimeSpan.Zero;
                    return (isLive ? MediaType.Livestream : MediaType.Video, vid);
                }
                catch (Exception) { throw new CriticalException("ID might not be valid."); }
            }

            if (url.Contains(@"https://www.youtube.com/playlist?list="))            
                return (MediaType.Playlist, yt.Playlists.GetAsync(url));
            
            throw new CriticalException("The mediatype could not be evaluated. Something has gone very wrong here... (int)");
        }

        public Task<MediaType> EvaluateMediaTypeAsync(string input)
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
                return Task.FromResult(MediaType.Video);

            if (input.Contains(@"https://www.youtube.com/playlist?list="))
                return Task.FromResult(MediaType.Playlist);

            throw new CriticalException("MediaType could not be evaluated. (YT)");
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

        public Task<MediaMetadata> GetMediaInfoAsync(string url)
        {
            url = SearchOrGetVideo(url).Result;
            var (mType, obj) = InternalEvaluateMediaType(url).Result;

            return mType switch
            {
                MediaType.Video => GetVideoMetadataAsync((Video)obj),
                MediaType.Playlist => GetPlaylistMetadataAsync((Playlist)obj),
                MediaType.Livestream => Task.FromResult((MediaMetadata)obj),
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
