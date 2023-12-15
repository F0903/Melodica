using Melodica.Config;
using Melodica.Services.Audio;
using Melodica.Services.Caching;
using Melodica.Services.Downloaders.Exceptions;
using Melodica.Services.Media;
using Melodica.Utility;

using Soundclouder;
using Soundclouder.Entities;

using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Melodica.Services.Downloaders.SoundCloud;

internal sealed partial class AsyncSoundcloudDownloader : IAsyncDownloader
{
    [GeneratedRegex(@"(((https)|(http)):\/\/)?soundcloud\.com\/{1}.+\/{1}.+", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex SoundcloudUrlRegex();

    private static readonly SearchClient search = new(BotConfig.Secrets.SoundcloudClientID);

    private static readonly IMediaCache cache = new MediaFileCache("Soundcloud");

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

    static LazyMedia CreateLazyMedia(MediaInfo info, MediaInfo? collectionInfo = null)
    {
        PlayableMedia media = new(info, collectionInfo, static async (x) =>
        {
            var tracks = await search.GetTracksAsync(x.Info.Id);
            var track = tracks[0];
            var streamUrl = await track.GetStreamURLAsync();
            using FFmpegProcessor ffmpeg = new(streamUrl, "hls");
            var procStreams = await ffmpeg.ProcessAsync();
            return new DataPair(procStreams.Output, "s16le");
        }, cache);
        return new LazyMedia(media);
    }

    static Task<MediaCollection> DownloadTrackAsync(MediaInfo info)
    {
        var media = CreateLazyMedia(info);
        return Task.FromResult(new MediaCollection(media));
    }

    static async Task<MediaCollection> DownloadPlaylistAsync(MediaInfo info)
    {
        var result = await search.ResolveAsync(info.Url ?? throw new NullReferenceException("Playlist url was null!"));
        var playlist = result switch
        {
            PlaylistResolveResult prs => prs.Playlist,
            _ => throw new UnreachableException(),
        };

        List<LazyMedia> tracks = [];
        foreach (var track in playlist.Tracks)
        {
            var trackInfo = TrackToMediaInfo(track);
            var media = CreateLazyMedia(trackInfo, info);
            tracks.Add(media);
        }
        MediaCollection collection = new(tracks, info);
        return collection;
    }

    public Task<MediaCollection> DownloadAsync(MediaInfo info)
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
