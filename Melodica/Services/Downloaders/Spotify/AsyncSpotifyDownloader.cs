using System.Text.RegularExpressions;

using Melodica.Config;
using Melodica.Services.Downloaders.Exceptions;
using Melodica.Services.Media;
using Melodica.Utility;

using SpotifyAPI.Web;

namespace Melodica.Services.Downloaders.Spotify;

public sealed partial class AsyncSpotifyDownloader : IAsyncDownloader
{
    [GeneratedRegex(@"((http)|(https)):\/\/((api)|(open))\.spotify\.com(\/v\d+)?\/.+\/.+", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.Singleline)]
    private static partial Regex SpotifyUrlRegex(); 

    static readonly SpotifyClient spotify = new(SpotifyClientConfig
        .CreateDefault()
        .WithAuthenticator(new ClientCredentialsAuthenticator(BotConfig.Secrets.SpotifyClientID, BotConfig.Secrets.SpotifyClientSecret)));

    // Tie this to the default downloader (can't download directly from Spotify)
    static readonly IAsyncDownloader downloader = DownloaderResolver.DefaultDownloader;

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
        List<PlaylistTrack<IPlayableItem>>? tracks = playlist.Tracks?.Items ?? throw new NullReferenceException("No tracks were found in playlist.");
        int trackCount = tracks.Count;
        FullTrack[]? tracklist = new FullTrack[trackCount];
        for (int i = 0; i < trackCount; i++)
        {
            IPlayableItem? track = tracks[i].Track;
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
        FullTrack[]? tracks = await PlaylistToTrackListAsync(playlist);
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
        string? id = await ParseURLToIdAsyncAsync(url.ToString());
        FullAlbum? album = await spotify.Albums.Get(id);
        return AlbumToMediaInfo(album);
    }

    static async Task<MediaInfo> GetPlaylistInfoAsync(ReadOnlyMemory<char> url)
    {
        string? id = await ParseURLToIdAsyncAsync(url.ToString());
        FullPlaylist? playlist = await spotify.Playlists.Get(id);
        return await PlaylistToMediaInfoAsync(playlist);
    }

    static async ValueTask<PlayableMedia> DownloadFromProviderAsync(MediaInfo info)
    {
        MediaInfo? extInfo = await downloader.GetInfoAsync($"{info.Artist} {info.Title}".AsMemory());
        MediaCollection? extMedia = await downloader.DownloadAsync(extInfo);
        PlayableMedia extVideo = extMedia.First();
        extVideo.Info = info with { Id = extVideo.Info.Id, Url = extVideo.Info.Url };
        return extVideo;
    }

    static async ValueTask<MediaCollection> DownloadSpotifyAlbumAsync(FullAlbum album)
    {
        MediaInfo? albumInfo = AlbumToMediaInfo(album);
        List<SimpleTrack>? tracks = await AlbumToTrackListAsync(album);
        int trackLength = tracks.Count;

        IEnumerable<LazyMedia> GetCollection()
        {
            foreach (SimpleTrack? track in tracks)
            {
                MediaGetter getter = () =>
                {
                    MediaInfo? info = SimpleTrackToMediaInfo(track, album);
                    PlayableMedia? video = DownloadFromProviderAsync(info).AsTask().Result;
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
        MediaInfo? playlistInfo = await PlaylistToMediaInfoAsync(playlist);
        FullTrack[]? tracks = await PlaylistToTrackListAsync(playlist);
        int trackLength = tracks.Length;

        IEnumerable<LazyMedia> GetCollection()
        {
            foreach (FullTrack? track in tracks)
            {
                MediaGetter getter = () =>
                {
                    MediaInfo? info = FullTrackToMediaInfo(track);
                    PlayableMedia? video = DownloadFromProviderAsync(info).AsTask().Result;
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
            FullAlbum? album = await spotify.Albums.Get(info.Id);
            return await DownloadSpotifyAlbumAsync(album);
        }
        catch { }

        try
        {
            FullPlaylist? playlist = await spotify.Playlists.Get(info.Id);
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

        PlayableMedia? media = await DownloadFromProviderAsync(info);
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

        string? id = await ParseURLToIdAsyncAsync(query.Span);
        FullTrack? track = await spotify.Tracks.Get(id);
        return FullTrackToMediaInfo(track);
    } 
}
