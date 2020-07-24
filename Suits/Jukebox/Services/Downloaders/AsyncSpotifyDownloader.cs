using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SpotifyAPI.Web;
using Suits.Jukebox.Models;
using Suits.Jukebox.Models.Exceptions;
using Suits.Jukebox.Models.Origins;
using Suits.Utility.Extensions;

namespace Suits.Jukebox.Services.Downloaders
{
    public class AsyncSpotifyDownloader : AsyncDownloaderBase
    {
        static readonly SpotifyClient spotify = new SpotifyClient(SpotifyClientConfig
                                                                  .CreateDefault()
                                                                  .WithAuthenticator(new ClientCredentialsAuthenticator("f8ecc5fd441249e4bc1471c5bfbb7cbd",
                                                                                                                        "83890edde8014ffd927bf98b6394d4a2")));

        // Tie this to the default downloader (can't download directly from Spotify)
        readonly AsyncDownloaderBase dlHelper = Default;

        private Task<string> ParseURLToId(string url)
        {
            if (!url.StartsWith("https://"))
                return Task.FromResult(url);
            var startIndex = url.LastIndexOf('/') + 1;
            var stopIndex = url.IndexOf('?');
            var id = url[startIndex..stopIndex];
            return Task.FromResult(id);
        }

        private Task<PlayableMedia> DownloadVideo(MediaMetadata info)
        {
            // Outsource downloading to another service (YouTube) since Spotify doesn't support direct streaming.
            var vidMeta = dlHelper.GetMediaInfoAsync(info.Title!).Result;

            return dlHelper.DownloadAsync(vidMeta);
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
            var id = await ParseURLToId(url);

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
                    throw new CriticalException("Album tracks could not be fetched.");                
                    
                for (int i = 0; i < tracks!.Count; i++)
                {
                    var track = tracks[i];
                    var trackTitle = $"{tracks[i].Artists[0].Name} {tracks[i].Name}";

                    var lastTotalDuration = totalDuration;

                    playlistTracks[i] = new MediaMetadata()
                    {
                        MediaOrigin = MediaOrigin.Spotify,
                        MediaType = MediaType.Video,
                        Title = trackTitle,
                        Duration = (totalDuration += TimeSpan.FromSeconds(track.DurationMs / 1000)) - lastTotalDuration, // Ugly
                        Thumbnail = itemImages?[0].Url,
                        URL = track.PreviewUrl,
                        ID = track.Id
                    };
                }
            }
            else // Stupid but neccesary due to API
            {
                var tracks = ToTrackList(spotifyPlaylist!.Tracks!.Items!);
                for (int i = 0; i < tracks.Count; i++)
                {
                    var track = tracks[i];
                    var trackTitle = $"{tracks[i].Artists[0].Name} {tracks[i].Name}";

                    var lastTotalDuration = totalDuration;

                    playlistTracks[i] = new MediaMetadata()
                    {
                        MediaOrigin = MediaOrigin.Spotify,
                        MediaType = MediaType.Video,
                        Title = trackTitle,
                        Duration = (totalDuration += TimeSpan.FromSeconds(track.DurationMs / 1000)) - lastTotalDuration, // Ugly
                        Thumbnail = itemImages?[0].Url,
                        URL = track.PreviewUrl,
                        ID = track.Id
                    };
                }
            }

            var playlistInfo = new MediaMetadata()
            {
                Title = (isAlbum ? spotifyAlbum!.Name : spotifyPlaylist!.Name) ?? throw new CriticalException("Could not fetch name of Spotify media."),
                Duration = totalDuration,
                MediaOrigin = MediaOrigin.Spotify,
                MediaType = MediaType.Playlist,
                Thumbnail = itemImages![0].Url
            };

            return (playlistInfo, playlistTracks);
        }

        public async Task<MediaMetadata> DownloadVideoInfoAsync(string url)
        {
            var id = await ParseURLToId(url);
            var track = await spotify.Tracks.Get(id);

            return new MediaMetadata()
            {
                MediaOrigin = MediaOrigin.Spotify,
                MediaType = MediaType.Video,
                Title = track.Name,
                Duration = TimeSpan.FromSeconds(track.DurationMs / 1000),
                ID = track.Id,
                URL = track.PreviewUrl,
                Thumbnail = track.Album.Images.FirstOrDefault().Url
            };
        }

        public override bool IsUrlSupported(string url)
        {
            if (url.Contains("https://open.spotify.com/"))
                return true;
            else return false;
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
    }
}
