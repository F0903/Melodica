using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Melodica.Services.Downloaders.Exceptions;
using Melodica.Services.Models;
using Melodica.Utility.Extensions;

using SpotifyAPI.Web;

namespace Melodica.Services.Downloaders.Spotify
{
    public class AsyncSpotifyDownloader : AsyncDownloaderBase
    {
        private static readonly SpotifyClient spotify = new SpotifyClient(SpotifyClientConfig
                                                                  .CreateDefault()
                                                                  .WithAuthenticator(new ClientCredentialsAuthenticator("f8ecc5fd441249e4bc1471c5bfbb7cbd",
                                                                                                                        "83890edde8014ffd927bf98b6394d4a2")));

        // Tie this to the default downloader (can't download directly from Spotify)
        private readonly AsyncDownloaderBase dlHelper = Default;

        public override bool IsUrlSupported(string url) => url.StartsWith("https://open.spotify.com/") ||
                                                           url.StartsWith("http://open.spotify.com/") ||
                                                           url.StartsWith("https://api.spotify.com/v1/") ||
                                                           url.StartsWith("http://api.spotify.com/v1/");

        private string SeperateArtistNames(List<SimpleArtist> artists)
        {
            string[]? nameArray = artists.ToArray().Convert(x => x.Name).ToArray();
            return nameArray.SeperateStrings();
        }

        private Task<string> ParseURLToIdAsync(ReadOnlySpan<char> url)
        {
            if (!(url.StartsWith("https://") || url.StartsWith("http://")))
                return Task.FromResult(url.ToString()); // Just return, cause the url is probably already an id.
            int startIndex = url.LastIndexOf('/') + 1;
            int qIndx = url.IndexOf('?');
            int stopIndex = qIndx == -1 ? url.Length : qIndx;
            string? id = url[startIndex..stopIndex].ToString();
            return Task.FromResult(id);
        }

        private async Task<PlayableMedia> DownloadVideo(MediaMetadata info)
        {
            // Outsource downloading to another service (YouTube) since Spotify doesn't support direct streaming.
            var video = await dlHelper.DownloadAsync($"{info.Artist} {info.Title}");
            video.Info.Id = info.Id;
            video.Info.Title = info.Title;
            video.Info.Artist = info.Artist;
            video.Info.Thumbnail = info.Thumbnail;
            return video;
        }

        public override Task<PlayableMedia> DownloadAsync(MediaMetadata info) => info.MediaType switch
        {
            MediaType.Video => DownloadVideo(info),
            MediaType.Playlist => throw new NotSupportedException(), // Playlist is not specified here due to the interface.
            MediaType.Livestream => throw new NotSupportedException(),
            _ => throw new NotSupportedException(),
        };

        public override Task<PlayableMedia> DownloadAsync(string url)
        {
            var info = GetMediaInfoAsync(url).Result;
            return DownloadAsync(info);
        }

        private List<FullTrack> ToTrackList(List<PlaylistTrack<IPlayableItem>> tracks)
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

        public override async Task<(MediaMetadata playlist, IEnumerable<MediaMetadata> videos)> DownloadPlaylistInfoAsync(string url)
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
            var playlistTracks = new MediaMetadata[isAlbum ? spotifyAlbum!.Tracks.Items!.Count : spotifyPlaylist!.Tracks!.Items!.Count];
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

                    playlistTracks[i] = new MediaMetadata()
                    {
                        Origin = MediaOrigin.Spotify,
                        MediaType = MediaType.Video,
                        Title = track.Name,
                        Artist = SeperateArtistNames(track.Artists),
                        Duration = (totalDuration += TimeSpan.FromSeconds(track.DurationMs / 1000)) - lastTotalDuration, // Ugly
                        Thumbnail = trackImages.Count > 0 ? trackImages[0].Url : null,
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

                    playlistTracks[i] = new MediaMetadata()
                    {
                        Origin = MediaOrigin.Spotify,
                        MediaType = MediaType.Video,
                        Title = track.Name,
                        Artist = SeperateArtistNames(track.Artists),
                        Duration = (totalDuration += TimeSpan.FromSeconds(track.DurationMs / 1000)) - lastTotalDuration, // Ugly
                        Thumbnail = trackImages.Count > 0 ? trackImages[0].Url : null,
                        Url = track.Href,
                        Id = track.Id
                    };
                }
            }

            var playlistInfo = new MediaMetadata()
            {
                Title = (isAlbum ? spotifyAlbum!.Name : spotifyPlaylist!.Name) ?? throw new DownloaderException("Could not fetch name of Spotify media."),
                Artist = isAlbum ? SeperateArtistNames(spotifyAlbum!.Artists) : spotifyPlaylist!.Name ?? throw new DownloaderException("Could not fetch name of Spotify media."),
                Duration = totalDuration,
                Origin = MediaOrigin.Spotify,
                MediaType = MediaType.Playlist,
                Thumbnail = itemImages!.Count > 0 ? itemImages![0].Url : null,
                Url = isAlbum ? spotifyAlbum!.Href : spotifyPlaylist!.Href
            };

            return (playlistInfo, playlistTracks);
        }

        public async Task<MediaMetadata> DownloadVideoInfoAsync(string url)
        {
            string? id = await ParseURLToIdAsync(url);
            var track = await spotify.Tracks.Get(id);
            var trackImages = track.Album.Images;

            return new MediaMetadata()
            {
                Origin = MediaOrigin.Spotify,
                MediaType = MediaType.Video,
                Title = track.Name,
                Artist = SeperateArtistNames(track.Artists),
                Duration = TimeSpan.FromSeconds(track.DurationMs / 1000),
                Id = track.Id,
                Url = track.Href,
                Thumbnail = trackImages.Count > 0 ? trackImages[0].Url : null
            };
        }

        public override bool IsUrlPlaylistAsync(string url) => url.Contains("open.spotify.com/playlist/") ||
                                                            url.Contains("open.spotify.com/album/") ||
                                                            url.StartsWith("api.spotify.com/v1/playlists") ||
                                                            url.StartsWith("api.spotify.com/v1/albums");

        protected override Task<MediaType> EvaluateMediaTypeAsync(string url)
        {
            if (!url.IsUrl())
                throw new DownloaderException("Function only accepts a url. Something very wrong happened here... (SP)");

            if (IsUrlPlaylistAsync(url))
                return Task.FromResult(MediaType.Playlist);

            if (url.Contains("open.spotify.com/track/") || url.Contains("api.spotify.com/v1/tracks/"))
                return Task.FromResult(MediaType.Video);

            throw new UnrecognizedUrlException("The link provided is not supported.");
        }

        public override Task<MediaMetadata> GetMediaInfoAsync(string url)
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

        public override async Task<bool> VerifyUrlAsync(string url)
        {
            try
            {
                await EvaluateMediaTypeAsync(url);
                return true;
            }
            catch (Exception) { }
            return false;
        }

        public override Task<string> GetLivestreamAsync(string streamURL) => throw new NotSupportedException("Spotify does not support livestreams.");
        //public override Task<PlayableMedia> DownloadToExistingMetaAsync(MediaMetadata meta) => throw new NotSupportedException("Spotify does not support direct streaming of data.");
    }
}
