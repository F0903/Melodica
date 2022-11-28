using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

using AngleSharp;
using AngleSharp.Dom;

using Melodica.Config;
using Melodica.Core.Exceptions;
using Melodica.Utility;

namespace Melodica.Services.Lyrics;

public class LyricsException : Exception
{
    public LyricsException(string? msg = null, Exception? inner = null) : base(msg, inner) { }
}

public class LyricsNotFoundException : LyricsException
{
    public LyricsNotFoundException(string query) : base($"No results found for lyrics search. Query was: ```{query}```", null) { }
}

public sealed partial class GeniusLyrics : ILyricsProvider
{
    static readonly HttpClient http = new() { DefaultRequestHeaders = { { "Authorization", $"Bearer {BotConfig.Secrets.GeniusToken}" } } };

    private static async Task<string> ParseLyricsAsync(string url)
    {
        var config = Configuration.Default.WithDefaultLoader();
        using var context = BrowsingContext.New(config);

        using var doc = await context.OpenAsync(url);

        IHtmlCollection<IElement>? lyricElements = null;
        int tries = 0;
        while (lyricElements == null)
        {
            if (tries > 5)
                throw new CriticalException("GeniusLyrics exceeded max attempts. Could not get lyrics content from html.");

            lyricElements = doc.QuerySelectorAll("div[data-lyrics-container=true]");

            Thread.Sleep(250 * tries);
            ++tries;
        }

        var strBuf = new StringBuilder(200);
        foreach (IElement? item in lyricElements)
            strBuf.Append(item.InnerHtml);
        strBuf.Replace("<br>", "\n");

        var finalStr = LyricsBrElementRegex().Replace(strBuf.ToString(), "");
        return finalStr;
    }

    private static async Task<LyricsInfo> SearchForSongAsync(string query)
    {
        var fixedQuery = query.UrlFriendlyfy();

        using var req = await http.GetAsync($"https://api.genius.com/search?q={fixedQuery}");

        using var responseStream = req.EnsureSuccessStatusCode().Content.ReadAsStream();
        using var fullResponse = await JsonDocument.ParseAsync(responseStream);

        var responseSection = fullResponse.RootElement.GetProperty("response");

        JsonElement GetElement(int index = 0)
        {
            if (index > 5) throw new Exception("Max attempts reached. Could not retrieve song info.");

            JsonElement hit;
            try { hit = responseSection.GetProperty("hits")[index]; }
            catch { throw new LyricsNotFoundException(query); }
            var hitType = hit.GetProperty("type");
            if (hitType.GetString() != "song")
                return GetElement(index++);
            else
                return hit.GetProperty("result");
        }

        var songInfo = GetElement();
        var songTitle = songInfo.GetProperty("full_title").GetString() ?? throw new NullReferenceException("Could not convert JSON property to string.");
        var songImg = songInfo.GetProperty("header_image_url").GetString() ?? throw new NullReferenceException("Could not convert JSON property to string.");
        var songLyrics = await ParseLyricsAsync(songInfo.GetProperty("url").GetString() ?? throw new NullReferenceException("Could not convert JSON property to string."));

        return new LyricsInfo { Image = songImg, Title = songTitle, Lyrics = songLyrics };
    }

    public async Task<LyricsInfo> GetLyricsAsync(string query)
    {
        var lyrics = await SearchForSongAsync(query);
        return lyrics;
    }

    [GeneratedRegex(@"<(?!\s*br\s*\/?)[^>]+>")]
    private static partial Regex LyricsBrElementRegex();
}
