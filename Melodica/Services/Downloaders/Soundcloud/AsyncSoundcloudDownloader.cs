using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using Melodica.Services.Caching;
using Melodica.Services.Downloaders.Exceptions;
using Melodica.Services.Models;

using SoundCloud.Api;
using SoundCloud.Api.Entities;
using SoundCloud.Api.Entities.Enums;

namespace Melodica.Services.Downloaders.Soundcloud
{
    public class AsyncSoundCloudDownloader : AsyncDownloaderBase
    {
        private static readonly Regex playlistRegex = new Regex(@"https:\/\/soundcloud\.com\/.+\/sets\/", RegexOptions.Compiled);
        private readonly ISoundCloudClient soundcloud = SoundCloudClient.CreateUnauthorized("1mJh51hV11v1prDWhy9hLmGaqvfrauWc");
        private readonly WebClient web = new WebClient();
        private readonly MediaFileCache cache = new MediaFileCache("SoundCloud");

        private async Task<PlayableMedia> DownloadTrackAsync(long trackId)
        {
            string? stringId = trackId.ToString();
            if (cache.Contains(stringId))
                await cache.GetAsync(stringId);

            var track = await soundcloud.Tracks.GetAsync(trackId);
            var trackInfo = GetTrackInfo(track);

            if (track.Downloadable.HasValue && !track.Downloadable.Value)
                throw new MediaUnavailableException("Track is not downloadable.");

            byte[]? trackBytes = web.DownloadData(track.DownloadUrl);
            var trackData = new MemoryStream(trackBytes);

            var media = new PlayableMedia(trackInfo, trackData);

            return await cache.CacheMediaAsync(media);
        }

        public override Task<PlayableMedia> DownloadAsync(string input)
        {
            var entt = soundcloud.Resolve.GetEntityAsync(input).Result;
            return entt.Kind switch
            {
                Kind.Track => DownloadTrackAsync(entt.Id),
                Kind.Playlist => throw new NotSupportedException("Playlists do not support direct download. Something wrong happened here. Please contact developer."),
                _ => throw new NotImplementedException(),
            };
        }

        public override Task<PlayableMedia> DownloadAsync(MediaMetadata input)
        {
            if (input.MediaType != MediaType.Video)
                throw new NotSupportedException("Playlists do not support direct download. Something wrong happened here. Please contact developer.");
            return DownloadTrackAsync(Convert.ToInt64(input.Id));
        }

        public override async Task<(MediaMetadata playlist, IEnumerable<MediaMetadata> videos)> DownloadPlaylistInfoAsync(string url)
        {
            var entt = await soundcloud.Resolve.GetEntityAsync(url);
            if (entt.Kind != Kind.Playlist)
                throw new UnrecognizedUrlException("Input did not resolve to a playlist.");

            var playlist = await soundcloud.Playlists.GetAsync(entt.Id);
            var playlistInfo = new MediaMetadata()
            {
                Artist = playlist.User.Username,
                Duration = TimeSpan.FromSeconds(playlist.Duration),
                Id = playlist.Id.ToString(),
                Title = playlist.Title,
                MediaType = MediaType.Playlist,
                Thumbnail = playlist.ArtworkUrl,
                Url = playlist.PermalinkUrl,
                Origin = MediaOrigin.SoundCloud
            };

            var tracks = new MediaMetadata[playlist.TrackCount];
            for (int i = 0; i < playlist.TrackCount; i++)
            {
                var plTrack = playlist.Tracks[i];
                tracks[i] = GetTrackInfo(plTrack);
            }

            return (playlistInfo, tracks);
        }

        public override Task<string> GetLivestreamAsync(string streamURL) => throw new NotSupportedException("SoundCloud does not support livestreams.");

        private MediaMetadata GetTrackInfo(Track track) => new MediaMetadata()
        {
            Artist = track.User.Username,
            Duration = TimeSpan.FromSeconds(track.Duration),
            Id = track.Id.ToString(),
            Title = track.Title,
            MediaType = MediaType.Video,
            Thumbnail = track.ArtworkUrl.AbsoluteUri,
            Url = track.PermalinkUrl.AbsoluteUri,
            Origin = MediaOrigin.SoundCloud
        };

        private Task<MediaMetadata> GetTrackInfoAsync(long id)
        {
            var track = soundcloud.Tracks.GetAsync(id).Result;
            return Task.FromResult(GetTrackInfo(track));
        }

        private async Task<MediaMetadata> GetPlaylistInfoAsync(long id)
        {
            var playlist = await soundcloud.Playlists.GetAsync(id);
            return new MediaMetadata()
            {
                Artist = playlist.User.Username,
                Duration = TimeSpan.FromSeconds(playlist.Duration),
                Id = playlist.Id.ToString(),
                Title = playlist.Title,
                MediaType = MediaType.Playlist,
                Thumbnail = playlist.ArtworkUrl,
                Url = playlist.PermalinkUrl,
                Origin = MediaOrigin.SoundCloud
            };
        }

        public override Task<MediaMetadata> GetMediaInfoAsync(string input)
        {
            var entt = soundcloud.Resolve.GetEntityAsync(input).Result;
            return entt.Kind switch
            {
                Kind.Playlist => GetPlaylistInfoAsync(entt.Id),
                Kind.Track => GetTrackInfoAsync(entt.Id),
                _ => throw new NotSupportedException("SoundCloud entity type is not supported. Please either try with a track or playlist/album.")
            };
        }

        public override bool IsUrlPlaylistAsync(string url) => playlistRegex.IsMatch(url);

        public override bool IsUrlSupported(string url) => url.StartsWith("https://soundcloud.com/");

        public override Task<bool> VerifyUrlAsync(string url)
        {
            if (url.Contains("https://soundcloud.com/"))
                return Task.FromResult(true);
            throw new UnrecognizedUrlException();
        }

        protected override async Task<MediaType> EvaluateMediaTypeAsync(string url)
        {
            var entt = await soundcloud.Resolve.GetEntityAsync(url);
            return entt.Kind switch
            {
                Kind.Playlist => MediaType.Playlist,
                Kind.Track => MediaType.Video,
                _ => throw new NotSupportedException()
            };
        }
    }
}