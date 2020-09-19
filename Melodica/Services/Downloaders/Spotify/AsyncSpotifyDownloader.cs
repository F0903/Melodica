using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Melodica.Core.Exceptions;
using Melodica.Services.Downloaders.Exceptions;
using Melodica.Services.Models;
using Melodica.Services.Services;
using Melodica.Utility.Extensions;

using SpotifyAPI.Web;

namespace Melodica.Services.Downloaders.Spotify
{
    public class AsyncSpotifyDownloader : AsyncDownloaderBase
    {
        static readonly SpotifyClient spotify = new SpotifyClient(SpotifyClientConfig
                                                                  .CreateDefault()
                                                                  .WithAuthenticator(new ClientCredentialsAuthenticator("f8ecc5fd441249e4bc1471c5bfbb7cbd",
                                                                                                                        "83890edde8014ffd927bf98b6394d4a2")));

        // Tie this to the default downloader (can't download directly from Spotify)
        readonly AsyncDownloaderBase dlHelper = Default;

        public override bool IsUrlSupported(string url) => url.StartsWith("https://open.spotify.com/") ||
                                                           url.StartsWith("http://open.spotify.com/") ||
                                                           url.StartsWith("https://api.spotify.com/v1/") ||
                                                           url.StartsWith("http://api.spotify.com/v1/");

        private Task<string> ParseURLToIdAsync(string url)
        {
            if (!(url.StartsWith("https://") || url.StartsWith("http://")))
                return Task.FromResult(url); // Just return, cause the url is probably already an id.
            var startIndex = url.LastIndexOf('/') + 1;
            var qIndx = url.IndexOf('?');
            var stopIndex = qIndx == -1 ? url.Length : qIndx;
            var id = url[startIndex..stopIndex];
            return Task.FromResult(id);
        }

        private async Task<PlayableMedia> DownloadVideo(MediaMetadata info)
        {
            // Outsource downloading to another service (YouTube) since Spotify doesn't support direct streaming.
            var video = await dlHelper.DownloadAsync($"{info.Artist} {info.Title}");
            video.Info.ID = info.ID;
            video.Info.Title = info.Title;
            video.Info.Artist = info.Artist;
            video.Info.Thumbnail = info.Thumbnail;
            return video;
        }

        public override Task<PlayableMedia> DownloadAsync(MediaMetadata info)
        {
            return info.MediaType switch
            {
                MediaType.Video => DownloadVideo(info),
                MediaType.Playlist => throw new NotSupportedException(), // Playlist is not specified here due to the interface.
                MediaType.Livestream => throw new NotSupportedException(),
                _ => throw new NotSupportedException(),
            };
        }

        public override Task<PlayableMedia> DownloadAsync(string url)
        {
            var info = GetMediaInfoAsync(url).Result;
            return DownloadAsync(info);
        }

        private List<FullTrack> ToTrackList(List<PlaylistTrack<IPlayableItem>> tracks)
        {
            List<FullTrack> tracklist = new List<FullTrack>();
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
            var id = await ParseURLToIdAsync(url);

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
            TimeSpan totalDuration = TimeSpan.Zero;

            if (isAlbum)
            {
                var tracks = spotifyAlbum!.Tracks.Items;
                if (tracks == null)
                    throw new DownloaderException("Album tracks could not be fetched.");

                for (int i = 0; i < tracks!.Count; i++)
                {
                    var track = tracks[i];

                    var lastTotalDuration = totalDuration;

                    playlistTracks[i] = new MediaMetadata()
                    {
                        MediaOrigin = MediaOrigin.Spotify,
                        MediaType = MediaType.Video,
                        Title = track.Name,
                        Artist = track.Artists.Aggregate<SimpleArtist, (string str, int indx)>(("", 0), (current, artist) => (current.str += current.indx != track.Artists.Count - 1 ? $"{artist.Name}, " : artist.Name, ++current.indx)).str,
                        Duration = (totalDuration += TimeSpan.FromSeconds(track.DurationMs / 1000)) - lastTotalDuration, // Ugly
                        Thumbnail = spotifyAlbum.Images[0].Url,
                        URL = track.Href,
                        ID = track.Id
                    };
                }
            }
            else
            {
                var tracks = ToTrackList(spotifyPlaylist!.Tracks!.Items!);
                for (int i = 0; i < tracks.Count; i++)
                {
                    var track = tracks[i];

                    var lastTotalDuration = totalDuration;

                    playlistTracks[i] = new MediaMetadata()
                    {
                        MediaOrigin = MediaOrigin.Spotify,
                        MediaType = MediaType.Video,
                        Title = track.Name,
                        Artist = track.Artists.Aggregate<SimpleArtist, (string str, int indx)>(("", 0), (current, artist) => (current.str += current.indx != track.Artists.Count - 1 ? $"{artist.Name}, " : artist.Name, ++current.indx)).str,
                        Duration = (totalDuration += TimeSpan.FromSeconds(track.DurationMs / 1000)) - lastTotalDuration, // Ugly
                        Thumbnail = track.Album.Images[0].Url,
                        URL = track.Href,
                        ID = track.Id
                    };
                }
            }

            var playlistInfo = new MediaMetadata()
            {
                Title = (isAlbum ? spotifyAlbum!.Name : spotifyPlaylist!.Name) ?? throw new DownloaderException("Could not fetch name of Spotify media."),
                Artist = isAlbum ? spotifyAlbum!.Artists.Aggregate<SimpleArtist, (string str, int indx)>(("", 0), (current, artist) => (current.str += current.indx != spotifyAlbum.Artists.Count - 1 ? $"{artist.Name}, " : artist.Name, ++current.indx)).str : spotifyPlaylist!.Name ?? throw new DownloaderException("Could not fetch name of Spotify media."),
                Duration = totalDuration,
                MediaOrigin = MediaOrigin.Spotify,
                MediaType = MediaType.Playlist,
                Thumbnail = itemImages![0].Url,
                URL = isAlbum ? spotifyAlbum!.Href : spotifyPlaylist!.Href
            };

            return (playlistInfo, playlistTracks);
        }

        public async Task<MediaMetadata> DownloadVideoInfoAsync(string url)
        {
            var id = await ParseURLToIdAsync(url);
            var track = await spotify.Tracks.Get(id);

            return new MediaMetadata()
            {
                MediaOrigin = MediaOrigin.Spotify,
                MediaType = MediaType.Video,
                Title = track.Name,
                Artist = track.Artists.Aggregate<SimpleArtist, (string str, int indx)>(("", 0), (current, artist) => (current.str += current.indx != track.Artists.Count - 1 ? $"{artist.Name}, " : artist.Name, ++current.indx)).str,
                Duration = TimeSpan.FromSeconds(track.DurationMs / 1000),
                ID = track.Id,
                URL = track.Href,
                Thumbnail = track.Album.Images.FirstOrDefault().Url
            };
        }

        public override bool IsPlaylistAsync(string url) => url.Contains("open.spotify.com/playlist/") ||
                                                            url.Contains("open.spotify.com/album/") ||
                                                            url.StartsWith("api.spotify.com/v1/playlists") ||
                                                            url.StartsWith("api.spotify.com/v1/albums");

        protected override Task<MediaType> EvaluateMediaTypeAsync(string url)
        {
            if (!url.IsUrl())
                throw new DownloaderException("Function only accepts a url. Something very wrong happened here... (SP)");

            if (IsPlaylistAsync(url))
                return Task.FromResult(MediaType.Playlist);

            if (url.Contains("open.spotify.com/track/") || url.Contains("api.spotify.com/v1/tracks/"))
                return Task.FromResult(MediaType.Video);

            throw new DownloaderException("The link provided is not supported.");
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

        public override async Task<bool> VerifyURLAsync(string url)
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
