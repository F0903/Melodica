using AngleSharp.Text;

using Melodica.Services.Caching;
using Melodica.Services.Downloaders.Exceptions;
using Melodica.Services.Media;
using Melodica.Utility;

using System.Text.RegularExpressions;

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

    static async Task<MediaInfo> GetInfoFromUrlAsync(ReadOnlyMemory<char> url)
    {
        if (IsUrlPlaylist(url.Span))
            return await GetInfoFromPlaylistUrlAsync(url);

        var id = VideoId.Parse(url.ToString());
        var video = await yt.Videos.GetAsync(id);
        return VideoToMetadata(video);
    }

    public async Task<MediaInfo> GetInfoAsync(ReadOnlyMemory<char> query)
    {
        if (query.IsUrl())
            return await GetInfoFromUrlAsync(query);

        var videos = yt.Search.GetVideosAsync(query.ToString());
        var result = await videos.FirstAsync();
        var video = await yt.Videos.GetAsync(result.Id);
        return VideoToMetadata(video);
    }

    static async Task<MediaCollection> DownloadLivestream(MediaInfo info)
    {
        var streamUrl = await yt.Videos.Streams.GetHttpLiveStreamUrlAsync(info.Id ?? throw new NullReferenceException("Id was null."));
        StreamableMedia media = new(info, streamUrl, "hls");
        return new MediaCollection(media);
    }

    static async Task<MediaCollection> DownloadPlaylist(MediaInfo info)
    {
        if (info.Id is null)
            throw new NullReferenceException("Id was null.");

        List<LazyMedia> videos = new(10);
        await foreach (var video in yt.Playlists.GetVideosAsync(info.Id))
        {
            if (video is null)
                continue;

            static async Task<DataPair> DataGetter(PlayableMedia self)
            {
                var manifest = await yt.Videos.Streams.GetManifestAsync(self.Info.Id);
                var streamInfo = manifest.GetAudioOnlyStreams().GetWithHighestBitrate();
                if (streamInfo is null)
                    throw new MediaUnavailableException("Media was unavailable.");
                var format = streamInfo.Container.Name.ToLower();

                try
                {
                    using var stream = await yt.Videos.Streams.GetAsync(streamInfo);
                    return new(stream, format);
                }
                catch (Exception ex) when (IsUnavailable(ex))
                {
                    throw new MediaUnavailableException("Video was unavailable.", ex);
                }
            }

            var vidInfo = VideoToMetadata(video);
            PlayableMedia? media = new(vidInfo, info, DataGetter, cache);
            videos.Add(media);
        }
        return new MediaCollection(videos, info);
    }

    public async Task<MediaCollection> DownloadAsync(MediaInfo info)
    {
        if (info.MediaType == MediaType.Playlist)
            return await DownloadPlaylist(info);

        if (info.MediaType == MediaType.Livestream)
            return await DownloadLivestream(info);

        if (info.Id is null)
            throw new NullReferenceException("Id was null.");

        static async Task<DataPair> DataGetter(PlayableMedia self)
        {
            var manifest = await yt.Videos.Streams.GetManifestAsync(self.Info.Id ?? throw new NullReferenceException("Id was null"));
            var streamInfo = manifest.GetAudioOnlyStreams().GetWithHighestBitrate() ?? throw new NullReferenceException("Could not get stream from YouTube.");
            using var stream = await yt.Videos.Streams.GetAsync(streamInfo);
            var format = streamInfo.Container.Name.ToLower();
            return new(stream, format);
        }

        PlayableMedia? media = new(info, null, DataGetter, cache);
        return new MediaCollection(media);
    }
}
