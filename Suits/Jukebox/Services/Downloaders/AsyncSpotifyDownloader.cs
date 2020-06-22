using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Suits.Jukebox.Models;
using SpotifyAPI.Web;
using YoutubeExplode;
using System.Linq;
using AngleSharp.Html.Dom;
using System.Runtime.CompilerServices;
using Suits.Utility.Extensions;

namespace Suits.Jukebox.Services.Downloaders
{
    public class AsyncSpotifyDownloader : IAsyncDownloader
    {
        public AsyncSpotifyDownloader()
        {
            var config = SpotifyClientConfig
                         .CreateDefault()
                         .WithAuthenticator(new ClientCredentialsAuthenticator("f8ecc5fd441249e4bc1471c5bfbb7cbd", "83890edde8014ffd927bf98b6394d4a2"));
            spotify = new SpotifyClient(config);
        }

        readonly SpotifyClient spotify;

        // Tie this to the default downloader (can't download directly from Spotify)
        readonly IAsyncDownloader dlHelper = IAsyncDownloader.Default;

        private Task<string> ParseURL(string url)
        {
            if (!url.StartsWith("https://"))
                return Task.FromResult(url);
            var startIndex = url.LastIndexOf('/') + 1;
            var stopIndex = url.IndexOf('?');
            var id = url[startIndex..stopIndex];
            return Task.FromResult(id);
        }

        private Task<PlayableMedia> DownloadVideo(string id)
        {
            var track = spotify.Tracks.Get(id).Result;
            return dlHelper.DownloadAsync($"{track.Artists[0].Name} {track.Name}");
        }

        public Task<PlayableMedia> DownloadAsync(string query) // Query is URL.
        {
            var mType = EvaluateMediaTypeAsync(query).Result;

            var id = ParseURL(query).Result;
            return mType switch
            {
                MediaType.Video => DownloadVideo(id),
                MediaType.Playlist => throw new NotSupportedException(), // Playlist is not specified here due to the interface.
                MediaType.Livestream => throw new NotSupportedException(),
                _ => throw new NotSupportedException(),
            };
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

        public async Task<(MediaMetadata playlist, IEnumerable<MediaMetadata> videos)> DownloadPlaylistInfoAsync(string url)
        {
            var id = await ParseURL(url);

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
            TimeSpan totalDuration = TimeSpan.Zero;
            var playlistTracks = new MediaMetadata[isAlbum ? spotifyAlbum!.Tracks.Items.Count : spotifyPlaylist!.Tracks.Items.Count];

            if (isAlbum)
            {
                var items = spotifyAlbum!.Tracks.Items;
                
                for (int i = 0; i < items.Count; i++)
                {
                    var trackTitle = $"{items[i].Artists[0].Name} {items[i].Name}";

                    var lastTotalDuration = totalDuration;

                    var helperMediaInfo = await dlHelper.GetMediaInfoAsync(trackTitle);
                    playlistTracks[i] = new MediaMetadata()
                    {
                        MediaOrigin = MediaOrigin.Spotify,
                        MediaType = MediaType.Video,
                        Title = helperMediaInfo.Title,
                        Duration = (totalDuration += helperMediaInfo.Duration) - lastTotalDuration, // Ugly
                        Thumbnail = helperMediaInfo.Thumbnail,
                        URL = helperMediaInfo.URL,
                        ID = helperMediaInfo.ID
                    };
                }
            }
            else // Stupid but neccesary due to API
            {
                var items = ToTrackList(spotifyPlaylist!.Tracks.Items);
                for (int i = 0; i < items.Count; i++)
                {
                    var trackTitle = $"{items[i].Artists[0].Name} {items[i].Name}";

                    var lastTotalDuration = totalDuration;

                    var ytItem = await dlHelper.GetMediaInfoAsync(trackTitle);
                    playlistTracks[i] = new MediaMetadata()
                    {
                        MediaOrigin = MediaOrigin.Spotify,
                        MediaType = MediaType.Video,
                        Title = ytItem.Title,
                        Duration = (totalDuration += ytItem.Duration) - lastTotalDuration, // Ugly
                        Thumbnail = ytItem.Thumbnail,
                        URL = ytItem.URL,
                        ID = ytItem.ID
                    };
                }
            }

            var playlistInfo = new MediaMetadata()
            {
                Title = isAlbum ? spotifyAlbum!.Name : spotifyPlaylist!.Name,
                Duration = totalDuration,
                MediaOrigin = MediaOrigin.Spotify,
                MediaType = MediaType.Playlist,
                Thumbnail = itemImages[0].Url
            };

            return (playlistInfo, playlistTracks);
        }

        public async Task<MediaMetadata> DownloadVideoInfoAsync(string url)
        {
            var id = await ParseURL(url);
            var track = await spotify.Tracks.Get(id);
            var ytTrack = await dlHelper.GetMediaInfoAsync($"{track.Artists[0].Name} {track.Name}");

            return new MediaMetadata()
            {
                MediaOrigin = MediaOrigin.Spotify,
                MediaType = MediaType.Video,
                Title = ytTrack.Title,
                Duration = ytTrack.Duration,
                ID = ytTrack.ID,
                URL = ytTrack.URL,
                Thumbnail = ytTrack.Thumbnail
            };
        }

        public Task<MediaType> EvaluateMediaTypeAsync(string url)
        {
            if (!url.IsUrl())
                throw new Exception("Function only accepts a url. Something very wrong happened here... (SP)");

            if (url.Contains(@"https://open.spotify.com/playlist/") || url.Contains(@"https://open.spotify.com/album/"))
                return Task.FromResult(MediaType.Playlist);

            if (url.Contains(@"https://open.spotify.com/track/"))
                return Task.FromResult(MediaType.Video);
            
            throw new NotSupportedException("The link provided is not supported.");
        }

        public Task<MediaMetadata> GetMediaInfoAsync(string url)
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

        public async Task<bool> VerifyURLAsync(string url)
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
    }
}
