using System.Text.RegularExpressions;
using AngleSharp.Text;
using Melodica.Services.Caching;
using Melodica.Services.Downloaders.Exceptions;
using Melodica.Services.Media;
using Melodica.Utility;
using YoutubeExplode;
using YoutubeExplode.Playlists;
using YoutubeExplode.Videos;
using YoutubeExplode.Videos.Streams;

namespace Melodica.Services.Downloaders.YouTube;

public sealed partial class AsyncYoutubeDownloader : IAsyncDownloader
{
    [GeneratedRegex(@"((http)|(https)):\/\/(www\.)?((youtube\.com\/((watch\?v=)|(playlist\?list=)))|(youtu\.be\/)).+", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex YoutubeUrlRegex();

    private static readonly YoutubeClient yt = new();

    private static readonly MediaFileCache cache = new("YouTube");

    static async Task<TimeSpan> GetTotalDurationAsync(Playlist pl)
    {
        var videos = yt.Playlists.GetVideosAsync(pl.Id);
        TimeSpan ts = new();
        await foreach (var video in videos)
        {
            if (video.Duration is null)
                continue;
            ts += video.Duration.Value;
        }
        return ts;
    }

    static async Task<string> GetPlaylistThumbnail(Playlist pl)
    {
        var videos = yt.Playlists.GetVideosAsync(pl.Id);
        var video = await videos.FirstAsync();
        return video.Thumbnails[0].Url;
    }


    static MediaInfo VideoToMetadata(Video video)
    {
        (var artist, var title) = video.Title.AsSpan().SeperateArtistName();
        return new MediaInfo(video.Id)
        {
            Title = title,
            Artist = artist,
            Duration = video.Duration ?? TimeSpan.Zero,
            Url = video.Url,
            ImageUrl = video.Thumbnails[0].Url,
            MediaType = video.Duration != TimeSpan.Zero ? MediaType.Video : MediaType.Livestream
        };
    }

    static MediaInfo VideoToMetadata(PlaylistVideo video)
    {
        (var artist, var title) = video.Title.AsSpan().SeperateArtistName();
        return new MediaInfo(video.Id)
        {
            Title = title,
            Artist = artist,
            Duration = video.Duration ?? TimeSpan.Zero,
            Url = video.Url,
            ImageUrl = video.Thumbnails[0].Url,
            MediaType = MediaType.Video
        };
    }

    static async Task<MediaInfo> PlaylistToMetadataAsync(Playlist pl)
    {
        var author = pl.Author?.ChannelTitle ?? "Unknown Artist";
        (var artist, var newTitle) = pl.Title.AsSpan().SeperateArtistName(author);
        return new MediaInfo(pl.Id)
        {
            MediaType = MediaType.Playlist,
            Duration = await GetTotalDurationAsync(pl),
            ImageUrl = await GetPlaylistThumbnail(pl),
            Title = newTitle,
            Artist = artist,
            Url = pl.Url
        };
    }

    static bool IsUnavailable(Exception ex)
    {
        return ex is YoutubeExplode.Exceptions.VideoUnplayableException or
               YoutubeExplode.Exceptions.VideoUnavailableException or
               YoutubeExplode.Exceptions.VideoRequiresPurchaseException;
    }

    static bool IsUrlPlaylist(ReadOnlySpan<char> url) => url.Contains("playlist", StringComparison.Ordinal);

    public bool IsUrlSupported(ReadOnlySpan<char> url) => YoutubeUrlRegex().IsMatch(url.ToString());

    static async Task<MediaInfo> GetInfoFromPlaylistUrlAsync(ReadOnlyMemory<char> url)
    {
        var id = PlaylistId.Parse(url.ToString());
        var playlist = await yt.Playlists.GetAsync(id);
        return await PlaylistToMetadataAsync(playlist);
    }

    static async Task<MediaInfo> GetInfoFromIdAsync(ReadOnlyMemory<char> id)
    {
        if (await cache.TryGetInfoAsync(id.ToString()) is var cachedInfo && cachedInfo is not null)
        {
            return cachedInfo;
        }

        var video = await yt.Videos.GetAsync(id.ToString());
        return VideoToMetadata(video);
    }

    static async Task<MediaInfo> GetInfoFromUrlAsync(ReadOnlyMemory<char> url)
    {
        if (IsUrlPlaylist(url.Span))
            return await GetInfoFromPlaylistUrlAsync(url);

        var id = VideoId.Parse(url.ToString());
        return await GetInfoFromIdAsync(id.Value.AsMemory());
    }

    public async Task<MediaInfo> GetInfoAsync(ReadOnlyMemory<char> query)
    {
        if (query.IsUrl())
            return await GetInfoFromUrlAsync(query);

        var videos = yt.Search.GetVideosAsync(query.ToString());
        var result = await videos.FirstAsync();
        return await GetInfoFromIdAsync(result.Id.Value.AsMemory());
    }

    static async Task<PlayableMediaStream> DownloadLivestream(MediaInfo info)
    {
        var streamUrl = await yt.Videos.Streams.GetHttpLiveStreamUrlAsync(info.Id);
        using var http = new HttpClient();
        var bytes = await http.GetByteArrayAsync(streamUrl);
        var stream = new MemoryStream(bytes, false);
        info.ExplicitDataFormat = "hls";
        var media = new PlayableMediaStream(stream, info, null, cache);
        return media;
    }

    internal async Task<Stream> GetMediaHttpStreamAsync(MediaInfo info)
    {
        var manifest = await yt.Videos.Streams.GetManifestAsync(info.Id);
        var streamInfo = manifest.GetAudioOnlyStreams().GetWithHighestBitrate() ?? throw new NullReferenceException("Could not get stream from YouTube.");
        return await yt.Videos.Streams.GetAsync(streamInfo);
    }

    async Task<PlayableMediaStream> GetPlayableMediaFromInfoAsync(MediaInfo info)
    {
        if (await cache.TryGetAsync(info.Id) is var cachedMedia && cachedMedia is not null)
        {
            return cachedMedia;
        }

        Stream stream;
        try
        {
            stream = await GetMediaHttpStreamAsync(info);
        }
        catch (Exception ex) when (IsUnavailable(ex))
        {
            throw new MediaUnavailableException("Video was unavailable.", ex);
        }
        return new PlayableMediaStream(stream, info, null, cache);
    }

    static async Task<PlayableMediaStream> DownloadPlaylist(MediaInfo info)
    {
        var videos = yt.Playlists.GetVideosAsync(info.Id);
        PlayableMediaStream? first = null;
        PlayableMediaStream? current = null;
        await foreach (var video in videos)
        {
            if (video is null)
                continue;

            static async Task<Stream> DataGetter(MediaInfo info)
            {
                try
                {
                    var manifest = await yt.Videos.Streams.GetManifestAsync(info.Id);
                    var streamInfo = manifest.GetAudioOnlyStreams().GetWithHighestBitrate() ?? throw new NullReferenceException("Could not get stream from YouTube.");
                    return await yt.Videos.Streams.GetAsync(streamInfo);
                }
                catch (Exception ex) when (IsUnavailable(ex))
                {
                    throw new MediaUnavailableException("Video was unavailable.", ex);
                }
            }

            var media = new PlayableMediaStream(
                (Func<MediaInfo,Task<Stream>>)DataGetter,
                (Func<Task<MediaInfo>>)(() => VideoToMetadata(video).WrapTask()),
                null,
                cache
            );
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

    public async Task<PlayableMediaStream> DownloadAsync(MediaInfo info)
    {
        if (info.MediaType == MediaType.Playlist)
            return await DownloadPlaylist(info);

        if (info.MediaType == MediaType.Livestream)
            return await DownloadLivestream(info);

        return await GetPlayableMediaFromInfoAsync(info);
    }
}
