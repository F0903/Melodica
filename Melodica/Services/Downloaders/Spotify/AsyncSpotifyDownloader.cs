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
    //TODO: Rewrite
    public class AsyncSpotifyDownloader : IAsyncDownloader
    {
        private readonly SpotifyClient spotify =
            new SpotifyClient(
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

        static Task<string> ParseURLToIdAsync(ReadOnlySpan<char> url)
        {
            if (!(url.StartsWith("https://") || url.StartsWith("http://")))
                return Task.FromResult(url.ToString()); // Just return, cause the url is probably already an id.
            int startIndex = url.LastIndexOf('/') + 1;
            int qIndx = url.IndexOf('?');
            int stopIndex = qIndx == -1 ? url.Length : qIndx;
            string? id = url[startIndex..stopIndex].ToString();
            return Task.FromResult(id);
        }

        static FullTrack[] ToTrackList(FullPlaylist playlist)
        {
            var tracks = playlist.Tracks?.Items ?? throw new NullReferenceException("No tracks were found in playlist.");
            var tracklist = new FullTrack[tracks.Count];
            for (int i = 0; i < tracks.Count; i++)
            {
                var track = tracks[i].Track;
                if (track is FullEpisode)
                    throw new NotSupportedException("Episodes are not supported. :(");
                tracklist[i] = ((FullTrack)track);
            }
            return tracklist;
        }

        static MediaInfo TrackToMediaInfo(FullTrack track)
        {
            return new MediaInfo()
            {
                Artist = SeperateArtistNames(track.Artists),
                Duration = TimeSpan.FromMilliseconds(track.DurationMs),
                Id = track.Id,
                ImageUrl = track.Album.Images.FirstOrDefault()?.Url,
                Url = track.PreviewUrl,
                MediaType = MediaType.Video,
                Title = track.Name
            };
        }

        static MediaInfo PlaylistToMediaInfo(FullPlaylist playlist)
        {
            var tracks = ToTrackList(playlist);
            return new MediaInfo()
            {
                Artist = (playlist.Owner?.DisplayName) ?? "Unknown",
                Duration = tracks.Sum(x => TimeSpan.FromMilliseconds(x.DurationMs)),
                Id = playlist.Id,
                ImageUrl = playlist.Images?.FirstOrDefault()?.Url,
                Url = playlist.ExternalUrls?.FirstOrDefault().Value,
                MediaType = MediaType.Playlist,
                Title = playlist.Name ?? "Unknown"
            };
        }

        static MediaInfo AlbumToMediaInfo(FullAlbum album)
        {
            return new MediaInfo()
            {
                Artist = SeperateArtistNames(album.Artists),
                Duration = album.Tracks.Items?.Sum(x => TimeSpan.FromMilliseconds(x.DurationMs)) ?? TimeSpan.FromMilliseconds(0),
                Id = album.Id,
                ImageUrl = album.Images?.FirstOrDefault()?.Url,
                Url = album.ExternalUrls.FirstOrDefault().Value,
                MediaType = MediaType.Playlist,
                Title = album.Name
            };
        }

        async Task<MediaInfo> GetAlbumInfoAsync(string url)
        {
            var id = await ParseURLToIdAsync(url);
            var album = await spotify.Albums.Get(id);
            return AlbumToMediaInfo(album);
        }

        async Task<MediaInfo> GetPlaylistInfoAsync(string url)
        {
            var id = await ParseURLToIdAsync(url);
            var playlist = await spotify.Playlists.Get(id);
            return PlaylistToMediaInfo(playlist);
        }

        public async Task<MediaInfo> GetInfoAsync(string query)
        {
            if (!query.IsUrl())
            {
                throw new NotSupportedException("Must be a valid spotify URL.");
            }

            if (IsUrlPlaylist(query))
            {
                return await GetPlaylistInfoAsync(query).ConfigureAwait(false);
            }

            if (IsUrlAlbum(query))
            {
                return await GetAlbumInfoAsync(query).ConfigureAwait(false);
            }

            var id = await ParseURLToIdAsync(query);
            var track = await spotify.Tracks.Get(id);
            return TrackToMediaInfo(track);
        }

        async Task<MediaCollection> DownloadPlaylistAsync(MediaInfo info)
        {
            //TODO:
            throw new NotImplementedException();
        }

        public async Task<MediaCollection> DownloadAsync(MediaInfo info)
        {
            if (info.MediaType == MediaType.Livestream)
                throw new NotSupportedException("Spotify does not support livestreams.");

            if(info.MediaType == MediaType.Playlist)
            {
                return await DownloadPlaylistAsync(info).ConfigureAwait(false);
            }

            //TODO: Impl caching

            var extInfo = await downloader.GetInfoAsync($"{info.Artist} {info.Title}");
            var extMedia = await downloader.DownloadAsync(extInfo);
            var extVideo = extMedia.First();
            extVideo.Info = info;
            return extMedia;
        }
    }
}