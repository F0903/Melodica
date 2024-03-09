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

    static async Task<PlayableMedia> CreatePlayableMediaAsync(MediaInfo info, MediaInfo? collectionInfo = null)
    {
        var tracks = await search.GetTracksAsync(info.Id);
        var streamUrl = await tracks[0].GetStreamURLAsync();
        var media = new PlayableMedia(new MemoryStream(Encoding.UTF8.GetBytes(streamUrl), false), info, null);
        return media;
    }

    static Task<PlayableMedia> DownloadTrackAsync(MediaInfo info)
    {
        return CreatePlayableMediaAsync(info);
    }

    static async Task<PlayableMedia> DownloadPlaylistAsync(MediaInfo info)
    {
        var result = await search.ResolveAsync(info.Url ?? throw new NullReferenceException("Playlist url was null!"));
        var playlist = result switch
        {
            PlaylistResolveResult prs => prs.Playlist,
            _ => throw new UnreachableException(),
        };

        PlayableMedia? first = null;
        PlayableMedia? current = null;
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
        }
        return first!;
    }

    public Task<PlayableMedia> DownloadAsync(MediaInfo info)
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
