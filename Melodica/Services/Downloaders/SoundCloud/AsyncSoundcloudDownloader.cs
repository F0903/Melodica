using System.Text.RegularExpressions;

using Discord;

using Melodica.Config;
using Melodica.Services.Audio;
using Melodica.Services.Caching;
using Melodica.Services.Media;
using Melodica.Utility;

using Soundclouder;

namespace Melodica.Services.Downloaders.SoundCloud;

internal sealed partial class AsyncSoundcloudDownloader : IAsyncDownloader
{
    [GeneratedRegex("((https)|(http)):\\/\\/soundcloud\\.com\\/.+\\/.+\\?", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex SoundcloudUrlRegex();

    private static readonly SearchClient search = new(new ClientInfo { ClientId = BotConfig.Secrets.SoundcloudClientID });

    private static readonly IMediaCache cache = new MediaFileCache("Soundcloud");

    private static MediaInfo ToMediaInfo(Soundclouder.Media media)
    {
        return new MediaInfo(media.ID.ToString())
        {
            Artist = media.Author,
            Duration = TimeSpan.FromSeconds(Math.Round(media.Duration.TotalSeconds)), // Don't include all the milliseconds.
            Title = media.Title,
            MediaType = MediaType.Video,
            ImageUrl = media.ArtworkUrl,
            Url = media.PermaLink,
        };
    }

    private static ReadOnlySpan<char> ParseSearchQueryFromUrl(ReadOnlySpan<char> url)
    {
        //https://soundcloud.com/phatdix/haachama-club-banger-3000?in=caspar-friis/sets/clubn-bagner&si=2233d96e6c3240eea190fbd28dd03e90&utm_source=clipboard&utm_medium=text&utm_campaign=social_sharing
        var numOfSlashes = 4;
        if (!(url.StartsWith("https") || url.StartsWith("http")))
        {
            numOfSlashes -= 2;
        }

        var slashIndex = 0;
        var encounters = 0;
        for (int i = 0; i < url.Length; i++)
        {
            var ch = url[i];
            if (ch != '/') continue;
            encounters++;
            if (encounters >= numOfSlashes)
            {
                slashIndex = i;
                break;
            }
        }

        var endIndex = url.Length;
        for (int i = slashIndex; i < url.Length; i++)
        {
            var ch = url[i];
            if (ch != '?') continue;
            endIndex = i;
            break;
        }

        var str = url.Slice(slashIndex + 1, endIndex - slashIndex - 1);
        return str;
    }

    public Task<MediaCollection> DownloadAsync(MediaInfo info)
    {
        if (info.Passthrough is null)
            throw new NullReferenceException("Soundcloud media was null.");

        var media = new PlayableMedia(info, null, static async (x) =>
        {
            using var http = new HttpClient();
            var streamUrl = (string)x.Info.Passthrough!;
            using var ffmpeg = new FFmpegAudioProcessor();
            await ffmpeg.StartProcess(new("hls", streamUrl));
            using var output = ffmpeg.GetOutput()!;
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
        if (query.IsUrl())
        {
            queryString = ParseSearchQueryFromUrl(queryString).ToString();
        }

        var result = await search.SearchAsync(queryString);
        var rawMedia = result.ReturnedMedia.First();
        var media = ToMediaInfo(rawMedia);
        media.Passthrough = await rawMedia.GetStreamURLAsync(); //Kinda smelly.
        return media;
    }

    public bool IsUrlSupported(ReadOnlySpan<char> url)
    {
        return SoundcloudUrlRegex().IsMatch(url.ToString());
    }
}
