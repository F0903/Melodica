using System;
using System.Collections.Generic;
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
        private readonly IAsyncDownloader dlHelper = IAsyncDownloader.Default;

        public bool IsUrlSupported(string url) =>
            url.StartsWith("https://open.spotify.com/") ||
            url.StartsWith("http://open.spotify.com/") ||
            url.StartsWith("https://api.spotify.com/v1/") ||
            url.StartsWith("http://api.spotify.com/v1/");

        private static string SeperateArtistNames(List<SimpleArtist> artists)
        {
            string[]? nameArray = artists.ToArray().Convert(x => x.Name).ToArray();
            return nameArray.SeperateStrings();
        }

        private static Task<string> ParseURLToIdAsync(ReadOnlySpan<char> url)
        {
            if (!(url.StartsWith("https://") || url.StartsWith("http://")))
                return Task.FromResult(url.ToString()); // Just return, cause the url is probably already an id.
            int startIndex = url.LastIndexOf('/') + 1;
            int qIndx = url.IndexOf('?');
            int stopIndex = qIndx == -1 ? url.Length : qIndx;
            string? id = url[startIndex..stopIndex].ToString();
            return Task.FromResult(id);
        }

        private async Task<PlayableMedia> DownloadVideo(MediaInfo info)
        {
            // Outsource downloading to another service (YouTube) since Spotify doesn't support direct streaming.
            var video = await dlHelper.DownloadAsync($"{info.Artist} {info.Title}");
            video.Info = video.Info with
            {
                Id = info.Id,
                Title = info.Title,
                Artist = info.Artist,
                ImageUrl = info.ImageUrl
            };
            return video;
        }

        public Task<PlayableMedia> DownloadAsync(MediaInfo info) => info.MediaType switch
        {
            MediaType.Video => DownloadVideo(info),
            MediaType.Playlist => throw new NotSupportedException(), // Playlist is not specified here due to the interface.
            MediaType.Livestream => throw new NotSupportedException(),
            _ => throw new NotSupportedException(),
        };

        public Task<PlayableMedia> DownloadAsync(string url)
        {
            var info = GetMediaInfoAsync(url).Result;
            return DownloadAsync(info);
        }

        private static List<FullTrack> ToTrackList(List<PlaylistTrack<IPlayableItem>> tracks)
        {
            var tracklist = new List<FullTrack>();
            foreach (var plTrack in tracks)
            {
                var track = plTrack.Track;
                if (track is FullEpisode)
                    throw new NotSupportedException("Episodes are not supported. :(");
                tracklist.Add((FullTrack)track);
            }
            return tracklist;
        }

        //TODO: Refactor or rewrite this whole ordeal.
        public async Task<(MediaInfo playlist, IEnumerable<MediaInfo> videos)> DownloadPlaylistInfoAsync(string url)
        {
            string? id = await ParseURLToIdAsync(url);

            bool isAlbum = false;

            FullPlaylist? spotifyPlaylist = null;
            try
            {
                spotifyPlaylist = await spotify.Playlists.Get(id);
            }
            catch (Exception) { isAlbum = true; }

            FullAlbum? spotifyAlbum = null;
            if (isAlbum)
            {
                try
                {
                    spotifyAlbum = await spotify.Albums.Get(id);
                }
                catch (Exception) { }
            }

            var itemImages = isAlbum ? spotifyAlbum!.Images : spotifyPlaylist!.Images;
            var playlistTracks = new SpotifyMediaInfo[isAlbum ? spotifyAlbum!.Tracks.Items!.Count : spotifyPlaylist!.Tracks!.Items!.Count];
            var totalDuration = TimeSpan.Zero;

            if (isAlbum)
            {
                var tracks = spotifyAlbum!.Tracks.Items;
                if (tracks == null)
                    throw new DownloaderException("Album tracks could not be fetched.");

                for (int i = 0; i < tracks!.Count; i++)
                {
                    var track = tracks[i];
                    var trackImages = spotifyAlbum.Images;

                    var lastTotalDuration = totalDuration;

                    playlistTracks[i] = new SpotifyMediaInfo()
                    {
                        MediaType = MediaType.Video,
                        Title = track.Name,
                        Artist = SeperateArtistNames(track.Artists),
                        Duration = (totalDuration += TimeSpan.FromSeconds(track.DurationMs / 1000)) - lastTotalDuration, // Ugly
                        ImageUrl = trackImages.Count > 0 ? trackImages[0].Url : null,
                        Url = track.Href,
                        Id = track.Id
                    };
                }
            }
            else
            {
                var tracks = ToTrackList(spotifyPlaylist!.Tracks!.Items!);
                for (int i = 0; i < tracks.Count; i++)
                {
                    var track = tracks[i];
                    var trackImages = track.Album.Images;

                    var lastTotalDuration = totalDuration;

                    playlistTracks[i] = new SpotifyMediaInfo()
                    {
                        MediaType = MediaType.Video,
                        Title = track.Name,
                        Artist = SeperateArtistNames(track.Artists),
                        Duration = (totalDuration += TimeSpan.FromSeconds(track.DurationMs / 1000)) - lastTotalDuration, // Ugly
                        ImageUrl = trackImages.Count > 0 ? trackImages[0].Url : null,
                        Url = track.Href,
                        Id = track.Id
                    };
                }
            }

            var playlistInfo = new SpotifyMediaInfo()
            {
                Title = (isAlbum ? spotifyAlbum!.Name : spotifyPlaylist!.Name) ?? throw new DownloaderException("Could not fetch name of Spotify media."),
                Artist = isAlbum ? SeperateArtistNames(spotifyAlbum!.Artists) : spotifyPlaylist!.Name ?? throw new DownloaderException("Could not fetch name of Spotify media."),
                Duration = totalDuration,
                MediaType = MediaType.Playlist,
                ImageUrl = itemImages!.Count > 0 ? itemImages![0].Url : null,
                Url = isAlbum ? spotifyAlbum!.Href : spotifyPlaylist!.Href
            };

            return (playlistInfo, playlistTracks);
        }

        public async Task<MediaInfo> DownloadVideoInfoAsync(string url)
        {
            string? id = await ParseURLToIdAsync(url);
            var track = await spotify.Tracks.Get(id);
            var trackImages = track.Album.Images;

            return new SpotifyMediaInfo()
            {
                MediaType = MediaType.Video,
                Title = track.Name,
                Artist = SeperateArtistNames(track.Artists),
                Duration = TimeSpan.FromSeconds(track.DurationMs / 1000),
                Id = track.Id,
                Url = track.Href,
                ImageUrl = trackImages.Count > 0 ? trackImages[0].Url : null
            };
        }

        public bool IsUrlPlaylistAsync(string url) => url.Contains("open.spotify.com/playlist/") ||
                                                            url.Contains("open.spotify.com/album/") ||
                                                            url.StartsWith("api.spotify.com/v1/playlists") ||
                                                            url.StartsWith("api.spotify.com/v1/albums");

        protected Task<MediaType> EvaluateMediaTypeAsync(string url)
        {
            if (!url.IsUrl())
                throw new DownloaderException("Function only accepts a url. Something very wrong happened here... (SP)");

            if (IsUrlPlaylistAsync(url))
                return Task.FromResult(MediaType.Playlist);

            if (url.Contains("open.spotify.com/track/") || url.Contains("api.spotify.com/v1/tracks/"))
                return Task.FromResult(MediaType.Video);

            throw new UnrecognizedUrlException("The link provided is not supported.");
        }

        public Task<MediaInfo> GetMediaInfoAsync(string url)
        {
            var mType = EvaluateMediaTypeAsync(url).Result;

            return mType switch
            {
                MediaType.Video => DownloadVideoInfoAsync(url),
                MediaType.Playlist => Task.FromResult(DownloadPlaylistInfoAsync(url).Result.playlist),
                MediaType.Livestream => throw new NotSupportedException(),
                _ => throw new NotSupportedException()
            };
        }

        public async Task<bool> VerifyUrlAsync(string url)
        {
            try
            {
                await EvaluateMediaTypeAsync(url);
                return true;
            }
            catch (Exception) { }
            return false;
        }

        public Task<string> GetLivestreamAsync(string streamURL) => throw new NotSupportedException("Spotify does not support livestreams.");
        Task<MediaType> IAsyncDownloader.EvaluateMediaTypeAsync(string url) => throw new NotSupportedException();
    }
}