using Melodica.Config;
using Melodica.Services.Downloaders.Exceptions;
using Melodica.Services.Media;
using Melodica.Utility;

using SpotifyAPI.Web;

using System.Text.RegularExpressions;

namespace Melodica.Services.Downloaders.Spotify;

public sealed partial class AsyncSpotifyDownloader : IAsyncDownloader
{
    [GeneratedRegex(@"((http)|(https)):\/\/((api)|(open))\.spotify\.com(\/v\d+)?\/.+\/.+", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.Singleline)]
    private static partial Regex SpotifyUrlRegex();

    static readonly SpotifyClient spotify = new(SpotifyClientConfig
        .CreateDefault()
        .WithAuthenticator(new ClientCredentialsAuthenticator(BotConfig.Secrets.SpotifyClientID, BotConfig.Secrets.SpotifyClientSecret)));

    // Tie this to the default downloader (can't download directly from Spotify)
    static readonly IAsyncDownloader downloader = Downloader.Default;

    static bool IsUrlPlaylist(ReadOnlySpan<char> url)
    {
        return url.Contains("playlist", StringComparison.Ordinal) ||
               url.StartsWith("playlists");
    }

    static bool IsUrlAlbum(ReadOnlySpan<char> url)
    {
        return url.Contains("album", StringComparison.Ordinal) ||
               url.StartsWith("albums");
    }

    public bool IsUrlSupported(ReadOnlySpan<char> url)
    {
        return SpotifyUrlRegex().IsMatch(url.ToString());
    }

    static string SeperateArtistNames(List<SimpleArtist> artists)
    {
        var nameArray = artists.ToArray().Convert(x => x.Name).ToArray();
        return nameArray.SeperateStrings();
    }

    static ValueTask<string> ParseURLToIdAsyncAsync(ReadOnlySpan<char> url)
    {
        if (!(url.StartsWith("https://") || url.StartsWith("http://")))
            return ValueTask.FromResult(url.ToString()); // Just return, cause the url is probably already an id.
        var startIndex = url.LastIndexOf('/') + 1;
        var qIndx = url.IndexOf('?');
        var stopIndex = qIndx == -1 ? url.Length : qIndx;
        var id = url[startIndex..stopIndex].ToString();
        return ValueTask.FromResult(id);
    }

    static ValueTask<FullTrack[]> PlaylistToTrackListAsync(FullPlaylist playlist)
    {
        var tracks = playlist.Tracks?.Items ?? throw new NullReferenceException("No tracks were found in playlist.");
        var trackCount = tracks.Count;
        var tracklist = new FullTrack[trackCount];
        for (var i = 0; i < trackCount; i++)
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

    static TimeSpan NormalizeTimeSpan(TimeSpan from)
    {
        return new(from.Hours, from.Minutes, from.Seconds);
    }

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

    static async Task<MediaInfo> GetAlbumInfoAsync(ReadOnlyMemory<char> url)
    {
        var id = await ParseURLToIdAsyncAsync(url.ToString());
        var album = await spotify.Albums.Get(id);
        return AlbumToMediaInfo(album);
    }

    static async Task<MediaInfo> GetPlaylistInfoAsync(ReadOnlyMemory<char> url)
    {
        var id = await ParseURLToIdAsyncAsync(url.ToString());
        var playlist = await spotify.Playlists.Get(id);
        return await PlaylistToMediaInfoAsync(playlist);
    }

    static async ValueTask<PlayableMedia> DownloadFromProviderAsync(MediaInfo info)
    {
        var extInfo = await downloader.GetInfoAsync($"{info.Artist} {info.Title}".AsMemory());
        var extMedia = await downloader.DownloadAsync(extInfo);
        PlayableMedia extVideo = extMedia.First();
        extVideo.Info = info with { Id = extVideo.Info.Id, Url = extVideo.Info.Url };
        return extVideo;
    }

    static async ValueTask<MediaCollection> DownloadSpotifyAlbumAsync(FullAlbum album)
    {
        var albumInfo = AlbumToMediaInfo(album);
        var tracks = await AlbumToTrackListAsync(album);
        var trackLength = tracks.Count;

        IEnumerable<LazyMedia> GetCollection()
        {
            foreach (var track in tracks)
            {
                MediaGetter getter = () =>
                {
                    var info = SimpleTrackToMediaInfo(track, album);
                    var video = DownloadFromProviderAsync(info).AsTask().Result;
                    video.CollectionInfo = albumInfo;
                    return video;
                };
                yield return getter;
            }
        }

        return new MediaCollection(GetCollection(), albumInfo);
    }

    static async ValueTask<MediaCollection> DownloadSpotifyPlaylistAsync(FullPlaylist playlist)
    {
        var playlistInfo = await PlaylistToMediaInfoAsync(playlist);
        var tracks = await PlaylistToTrackListAsync(playlist);
        var trackLength = tracks.Length;

        IEnumerable<LazyMedia> GetCollection()
        {
            foreach (var track in tracks)
            {
                MediaGetter getter = () =>
                {
                    var info = FullTrackToMediaInfo(track);
                    var video = DownloadFromProviderAsync(info).AsTask().Result;
                    video.CollectionInfo = playlistInfo;
                    return video;
                };
                yield return getter;
            }
        }

        return new MediaCollection(GetCollection(), playlistInfo);
    }

    static async Task<MediaCollection> DownloadPlaylistAsync(MediaInfo info)
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
            return await DownloadPlaylistAsync(info);

        var media = await DownloadFromProviderAsync(info);
        return new MediaCollection(media);
    }

    public async Task<MediaInfo> GetInfoAsync(ReadOnlyMemory<char> query)
    {
        if (!query.IsUrl())
            throw new UrlNotSupportedException("Must be a valid spotify URL.");

        if (IsUrlPlaylist(query.Span))
            return await GetPlaylistInfoAsync(query);

        if (IsUrlAlbum(query.Span))
            return await GetAlbumInfoAsync(query);

        var id = await ParseURLToIdAsyncAsync(query.Span);
        var track = await spotify.Tracks.Get(id);
        return FullTrackToMediaInfo(track);
    }
}
