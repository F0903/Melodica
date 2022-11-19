using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

using AngleSharp;
using AngleSharp.Dom;

using Melodica.Core.Exceptions;
using Melodica.Config;
using Melodica.Utility;

namespace Melodica.Services.Lyrics;

public sealed partial class GeniusLyrics : ILyricsProvider
{
    static readonly HttpClient http = new() { DefaultRequestHeaders = { { "Authorization", $"Bearer {BotConfig.Secrets.GeniusToken}" } } };

    private static async Task<string> ParseLyricsAsync(string url)
    {
        IConfiguration? config = Configuration.Default.WithDefaultLoader();
        IBrowsingContext? context = BrowsingContext.New(config);

        using IDocument? doc = await context.OpenAsync(url);

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

        StringBuilder str = new(200);
        foreach (IElement? item in lyricElements)
            str.Append(item.InnerHtml);
        str.Replace("<br>", "\n");

        string? finalStr = LyricsBrElementRegex().Replace(str.ToString(), "");
        return finalStr;
    }

    private static async Task<LyricsInfo> SearchForSongAsync(string query)
    {
        string? fixedQuery = query.FixURLWhitespace();

        HttpResponseMessage? req = await http.GetAsync($"https://api.genius.com/search?q={fixedQuery}");

        Stream? responseStream = req.EnsureSuccessStatusCode().Content.ReadAsStream();
        using JsonDocument? fullResponse = await JsonDocument.ParseAsync(responseStream);

        JsonElement responseSection = fullResponse.RootElement.GetProperty("response");

        JsonElement GetElement(int index = 0)
        {
            if (index > 5) throw new Exception("Max attempts reached. Could not retrieve song info.");

            JsonElement hit;
            try { hit = responseSection.GetProperty("hits")[index]; }
            catch { throw new CriticalException($"No results found for lyrics search. Query was: {query}"); }
            JsonElement hitType = hit.GetProperty("type");
            if (hitType.GetString() != "song")
                return GetElement(index++);
            else
                return hit.GetProperty("result");
        }

        JsonElement songInfo = GetElement();
        string songTitle = songInfo.GetProperty("full_title").GetString() ?? throw new NullReferenceException("Could not convert JSON property to string.");
        string songImg = songInfo.GetProperty("header_image_url").GetString() ?? throw new NullReferenceException("Could not convert JSON property to string.");
        string songLyrics = await ParseLyricsAsync(songInfo.GetProperty("url").GetString() ?? throw new NullReferenceException("Could not convert JSON property to string."));

        return new LyricsInfo { Image = songImg, Title = songTitle, Lyrics = songLyrics };
    }

    public async Task<LyricsInfo> GetLyricsAsync(string query)
    {
        LyricsInfo lyrics = await SearchForSongAsync(query);
        return lyrics;
    }

    [GeneratedRegex("<(?!\\s*br\\s*\\/?)[^>]+>")]
    private static partial Regex LyricsBrElementRegex();
}
