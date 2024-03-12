using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using Melodica.Config;
using Melodica.Services.Audio;
using Melodica.Services.Caching;
using Melodica.Services.Downloaders.Exceptions;
using Melodica.Services.Media;
using Melodica.Utility;
using Soundclouder;
using Soundclouder.Entities;

namespace Melodica.Services.Downloaders.SoundCloud;

internal sealed partial class AsyncSoundcloudDownloader : IAsyncDownloader
{
    [GeneratedRegex(@"(((https)|(http)):\/\/)?soundcloud\.com\/{1}.+\/{1}.+", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex SoundcloudUrlRegex();

    private static readonly SearchClient search = new(BotConfig.Secrets.SoundcloudClientID ?? throw new NullReferenceException("SoundCloud ID was null!"));

    private static readonly MediaFileCache cache = new("Soundcloud");

    private static MediaInfo PlaylistToMediaInfo(Playlist playlist)
    {
        return new MediaInfo(playlist.ID.ToString())
        {
            Title = playlist.Title,
            Artist = playlist.Author,
            Duration = TimeSpan.FromSeconds(Math.Round(playlist.Duration.TotalSeconds)), // Don't include all the milliseconds.
            ImageUrl = playlist.ArtworkUrl,
            Url = playlist.PermaLinkUrl,
            MediaType = MediaType.Playlist,
        };
    }

    private static MediaInfo TrackToMediaInfo(Track track)
    {
        return new MediaInfo(track.ID.ToString())
        {
            Artist = track.Author,
            Duration = TimeSpan.FromSeconds(Math.Round(track.Duration.TotalSeconds)), // Don't include all the milliseconds.
            Title = track.Title,
            MediaType = MediaType.Video,
            ImageUrl = track.ArtworkUrl,
            Url = track.PermaLink,
        };
    }

    static async Task<PlayableMediaStream> CreatePlayableMediaAsync(MediaInfo info, MediaInfo? collectionInfo = null)
    {
        var tracks = await search.GetTracksAsync(info.Id);
        var streamUrl = await tracks[0].GetStreamURLAsync();
        using var http = new HttpClient();
        var stream = await http.GetStreamAsync(streamUrl);
        info.ExplicitDataFormat = "hls";
        var media = new PlayableMediaStream(stream, info, null, cache);
        return media;
    }

    static async Task<PlayableMediaStream> DownloadTrackAsync(MediaInfo info)
    {
        if (await cache.TryGetAsync(info.Id) is var cachedMedia && cachedMedia is not null)
        {
            return cachedMedia;
        }
        return await CreatePlayableMediaAsync(info);
    }

    static async Task<PlayableMediaStream> DownloadPlaylistAsync(MediaInfo info)
    {
        var result = await search.ResolveAsync(info.Url ?? throw new NullReferenceException("Playlist url was null!"));
        var playlist = result switch
        {
            PlaylistResolveResult prs => prs.Playlist,
            _ => throw new UnreachableException(),
        };

        PlayableMediaStream? first = null;
        PlayableMediaStream? current = null;
        foreach (var track in playlist.Tracks)
        {
            var trackInfo = TrackToMediaInfo(track);
            var media = await CreatePlayableMediaAsync(trackInfo, info);
            if (first is null)
            {
                first = media;
                current = first;
                continue;
            }
            current!.Next = media;
            current = current.Next;
        }
        return first!;
    }

    public Task<PlayableMediaStream> DownloadAsync(MediaInfo info)
    {
        return info.MediaType switch
        {
            MediaType.Video => DownloadTrackAsync(info),
            MediaType.Playlist => DownloadPlaylistAsync(info),
            _ => throw new DownloaderException("SoundCloud does not support the provided media type!"),
        };
    }

    public async Task<MediaInfo> GetInfoAsync(ReadOnlyMemory<char> query)
    {
        var queryString = query.Span.ToString();
        MediaInfo? info = null;
        if (query.IsUrl())
        {
            var result = await search.ResolveAsync(queryString);
            if (result is TrackResolveResult trackResult)
            {
                var track = trackResult.Track;
                info = TrackToMediaInfo(track);
            }
            else if (result is PlaylistResolveResult playlistResult)
            {
                var playlist = playlistResult.Playlist;
                info = PlaylistToMediaInfo(playlist);
            }
        }
        else
        {
            var result = await search.SearchAsync(queryString, filterKind: ResolveKind.Track);
            var track = result.ReturnedMedia.First();
            info = TrackToMediaInfo(track);
        }

        return info is null ? throw new MediaUnavailableException($"No info was found for the specified query: {query}") : info;
    }

    public bool IsUrlSupported(ReadOnlySpan<char> url) => SoundcloudUrlRegex().IsMatch(url.ToString());
}
