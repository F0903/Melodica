using System.Text.RegularExpressions;

using Melodica.Config;
using Melodica.Services.Audio;
using Melodica.Services.Caching;
using Melodica.Services.Media;
using Melodica.Utility;

using Soundclouder;

namespace Melodica.Services.Downloaders.SoundCloud;

internal sealed partial class AsyncSoundcloudDownloader : IAsyncDownloader
{
    [GeneratedRegex(@"(((https)|(http)):\/\/)?soundcloud\.com\/{1}.+\/{1}.+", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex SoundcloudUrlRegex();

    private static readonly SearchClient search = new(BotConfig.Secrets.SoundcloudClientID);

    private static readonly IMediaCache cache = new MediaFileCache("Soundcloud");

    private static MediaInfo ToMediaInfo(Track track)
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

    public Task<MediaCollection> DownloadAsync(MediaInfo info)
    {
        if (info.Passthrough is null)
            throw new NullReferenceException("Soundcloud media was null.");

        var media = new PlayableMedia(info, null, static async (x) =>
        {
            using var http = new HttpClient();
            var streamUrl = (string)x.Info.Passthrough!;
            using var ffmpeg = new FFmpegProcessor(streamUrl, "hls");
            using var output = await ffmpeg.ProcessAsync();
            var converted = new MemoryStream();
            await output.CopyToAsync(converted);
            return new DataPair(converted, "s16le");
        }, cache);
        var lazyMedia = new LazyMedia(media);
        return Task.FromResult(new MediaCollection(media));
    }

    public async Task<MediaInfo> GetInfoAsync(ReadOnlyMemory<char> query)
    {
        var queryString = query.Span.ToString();
        Track? track = null;
        if (query.IsUrl())
        {
            var result = await search.ResolveAsync(queryString);
            if (result is TrackResolveResult trackResult)
            {
                track = trackResult.Track;
            }
        }
        else
        {
            var result = await search.SearchAsync(queryString);
            track = result.ReturnedMedia.First();
        }
        if (track == null)
            throw new Exception("Track was not available!");


        var media = ToMediaInfo(track);
        media.Passthrough = await track.GetStreamURLAsync(); //Kinda smelly.
        return media;
    }

    public bool IsUrlSupported(ReadOnlySpan<char> url)
    {
        return SoundcloudUrlRegex().IsMatch(url.ToString());
    }
}
