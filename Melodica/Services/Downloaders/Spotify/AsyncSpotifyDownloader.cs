using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Melodica.Core;
using Melodica.Services.Downloaders.Exceptions;
using Melodica.Services.Media;
using Melodica.Utility.Extensions;

using SpotifyAPI.Web;

namespace Melodica.Services.Downloaders.Spotify
{
    public class AsyncSpotifyDownloader : IAsyncDownloader
    {
        private readonly SpotifyClient spotify =
            new(
                SpotifyClientConfig
                .CreateDefault()
                .WithAuthenticator(
                    new ClientCredentialsAuthenticator(BotSecrets.SpotifyClientID, BotSecrets.SpotifyClientSecret)));

        // Tie this to the default downloader (can't download directly from Spotify)
        static readonly IAsyncDownloader downloader = IAsyncDownloader.Default;

        static bool IsUrlPlaylist(string url) =>
            url.Contains("open.spotify.com/playlist/") ||
            url.StartsWith("api.spotify.com/v1/playlists");

        static bool IsUrlAlbum(string url) =>
            url.Contains("open.spotify.com/album/") ||
            url.StartsWith("api.spotify.com/v1/albums");

        public bool IsUrlSupported(string url) =>
            url.StartsWith("https://open.spotify.com/") ||
            url.StartsWith("http://open.spotify.com/") ||
            url.StartsWith("https://api.spotify.com/v1/") ||
            url.StartsWith("http://api.spotify.com/v1/");

        static string SeperateArtistNames(List<SimpleArtist> artists)
        {
            string[]? nameArray = artists.ToArray().Convert(x => x.Name).ToArray();
            return nameArray.SeperateStrings();
        }

        static ValueTask<string> ParseURLToIdAsyncAsync(ReadOnlySpan<char> url)
        {
            if (!(url.StartsWith("https://") || url.StartsWith("http://")))
                return ValueTask.FromResult(url.ToString()); // Just return, cause the url is probably already an id.
            int startIndex = url.LastIndexOf('/') + 1;
            int qIndx = url.IndexOf('?');
            int stopIndex = qIndx == -1 ? url.Length : qIndx;
            string? id = url[startIndex..stopIndex].ToString();
            return ValueTask.FromResult(id);
        }

        static ValueTask<FullTrack[]> PlaylistToTrackListAsync(FullPlaylist playlist)
        {
            var tracks = playlist.Tracks?.Items ?? throw new NullReferenceException("No tracks were found in playlist.");
            var trackCount = tracks.Count;
            var tracklist = new FullTrack[trackCount];
            for (int i = 0; i < trackCount; i++)
            {
                var track = tracks[i].Track;
                if (track is FullEpisode)
                    throw new UrlNotSupportedException("Episodes are not supported. :(");
                tracklist[i] = (FullTrack)track;
            }
            return ValueTask.FromResult(tracklist);
        }

        static ValueTask<List<SimpleTrack>> AlbumToTrackListAsync(FullAlbum album)
        {
            return ValueTask.FromResult(album.Tracks.Items ?? throw new NullReferenceException("No tracks were found in playlist."));
        }

        static TimeSpan NormalizeTimeSpan(TimeSpan from) => new(from.Hours, from.Minutes, from.Seconds);

        static MediaInfo FullTrackToMediaInfo(FullTrack track)
        {
            return new MediaInfo(track.Id)
            {
                Artist = SeperateArtistNames(track.Artists),
                Duration = NormalizeTimeSpan(TimeSpan.FromMilliseconds(track.DurationMs)),
                ImageUrl = track.Album.Images.FirstOrDefault()?.Url,
                Url = track.PreviewUrl,
                MediaType = MediaType.Video,
                Title = track.Name
            };
        }

        static MediaInfo SimpleTrackToMediaInfo(SimpleTrack track, FullAlbum? imageSource)
        {
            return new MediaInfo(track.Id)
            {
                Artist = SeperateArtistNames(track.Artists),
                Duration = NormalizeTimeSpan(TimeSpan.FromMilliseconds(track.DurationMs)),
                ImageUrl = imageSource?.Images[0].Url,
                Url = track.PreviewUrl,
                MediaType = MediaType.Video,
                Title = track.Name
            };
        }

        static async ValueTask<MediaInfo> PlaylistToMediaInfoAsync(FullPlaylist playlist)
        {
            var tracks = await PlaylistToTrackListAsync(playlist);
            return new MediaInfo(playlist.Id ?? throw new NullReferenceException("Playlist id was null. (spotify)"))
            {
                Artist = (playlist.Owner?.DisplayName) ?? "Unknown",
                Duration = NormalizeTimeSpan(tracks.Sum(x => TimeSpan.FromMilliseconds(x.DurationMs))),
                ImageUrl = playlist.Images?.FirstOrDefault()?.Url,
                Url = playlist.ExternalUrls?.FirstOrDefault().Value,
                MediaType = MediaType.Playlist,
                Title = playlist.Name ?? "Unknown"
            };
        }

        static MediaInfo AlbumToMediaInfo(FullAlbum album)
        {
            return new MediaInfo(album.Id)
            {
                Artist = SeperateArtistNames(album.Artists),
                Duration = NormalizeTimeSpan(album.Tracks.Items?.Sum(x => TimeSpan.FromMilliseconds(x.DurationMs)) ?? TimeSpan.FromMilliseconds(0)),
                ImageUrl = album.Images?.FirstOrDefault()?.Url,
                Url = album.ExternalUrls.FirstOrDefault().Value,
                MediaType = MediaType.Playlist,
                Title = album.Name
            };
        }

        async Task<MediaInfo> GetAlbumInfoAsync(string url)
        {
            var id = await ParseURLToIdAsyncAsync(url);
            var album = await spotify.Albums.Get(id);
            return AlbumToMediaInfo(album);
        }

        async Task<MediaInfo> GetPlaylistInfoAsync(string url)
        {
            var id = await ParseURLToIdAsyncAsync(url);
            var playlist = await spotify.Playlists.Get(id);
            return await PlaylistToMediaInfoAsync(playlist);
        }

        static async ValueTask<PlayableMedia> DownloadFromProviderAsync(MediaInfo info)
        {
            var extInfo = await downloader.GetInfoAsync($"{info.Artist} {info.Title}");
            var extMedia = await downloader.DownloadAsync(extInfo);
            var extVideo = extMedia.First();
            extVideo.Info = info with { Id = extVideo.Info.Id };
            return extVideo;
        }

        static async ValueTask<MediaCollection> DownloadSpotifyAlbumAsync(FullAlbum album)
        {
            var albumInfo = AlbumToMediaInfo(album);
            var tracks = await AlbumToTrackListAsync(album);
            var trackLength = tracks.Count;
            PlayableMedia[] collection = new PlayableMedia[trackLength];
            for (int i = 0; i < trackLength; i++)
            {
                var track = tracks[i];
                var info = SimpleTrackToMediaInfo(track, album);
                var video = await DownloadFromProviderAsync(info);
                video.CollectionInfo = albumInfo;
                collection[i] = video;
            }
            return new MediaCollection(collection, albumInfo);
        }

        static async ValueTask<MediaCollection> DownloadSpotifyPlaylistAsync(FullPlaylist playlist)
        {
            var playlistInfo = await PlaylistToMediaInfoAsync(playlist);
            var tracks = await PlaylistToTrackListAsync(playlist);
            var trackLength = tracks.Length;
            PlayableMedia[] collection = new PlayableMedia[trackLength];
            for (int i = 0; i < trackLength; i++)
            {
                var track = tracks[i];
                var info = FullTrackToMediaInfo(track);
                var video = await DownloadFromProviderAsync(info);
                video.CollectionInfo = playlistInfo;
                collection[i] = video;
            }
            return new MediaCollection(collection, playlistInfo);
        }

        async Task<MediaCollection> DownloadPlaylistAsync(MediaInfo info)
        {
            try
            {
                var album = await spotify.Albums.Get(info.Id);
                return await DownloadSpotifyAlbumAsync(album);
            }
            catch { }

            try
            {
                var playlist = await spotify.Playlists.Get(info.Id);
                return await DownloadSpotifyPlaylistAsync(playlist);
            }
            catch { }

            throw new UrlNotSupportedException("Could not find matching playlist or album.");
        }

        public async Task<MediaCollection> DownloadAsync(MediaInfo info)
        {
            if (info.MediaType == MediaType.Livestream)
                throw new UrlNotSupportedException("Spotify does not support livestreams.");

            if (info.MediaType == MediaType.Playlist)
            {
                return await DownloadPlaylistAsync(info);
            }

            var media = await DownloadFromProviderAsync(info);
            return new MediaCollection(media);
        }

        public async Task<MediaInfo> GetInfoAsync(string query)
        {
            if (!query.IsUrl())
            {
                throw new UrlNotSupportedException("Must be a valid spotify URL.");
            }

            if (IsUrlPlaylist(query))
            {
                return await GetPlaylistInfoAsync(query);
            }

            if (IsUrlAlbum(query))
            {
                return await GetAlbumInfoAsync(query);
            }

            var id = await ParseURLToIdAsyncAsync(query);
            var track = await spotify.Tracks.Get(id);
            return FullTrackToMediaInfo(track);
        }
    }
}