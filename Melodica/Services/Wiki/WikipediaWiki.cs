using System.Text.Json;

using Melodica.Utility.Extensions;

namespace Melodica.Services.Wiki;

public sealed class WikipediaWiki : IWikiProvider
{
    static readonly HttpClient http = new() { DefaultRequestHeaders = { { "Accept", "application/json; charset=utf-8" } } };

    private const string wikipediaEndpoint = "https://en.wikipedia.org/api/rest_v1";
    private const string wikipediaSummaryEndpoint = wikipediaEndpoint + "/page/summary/";

    private static async Task<WikiElement> GetSummary(string pageName)
    {
        string? fullEndpoint = $"{wikipediaSummaryEndpoint}{pageName}?redirect=false";

        HttpResponseMessage? response = await http.GetAsync(fullEndpoint);
        response.EnsureSuccessStatusCode();

        using Stream? resStream = await response.Content.ReadAsStreamAsync();
        JsonDocument doc;
        try { doc = JsonDocument.Parse(resStream); }
        catch { throw new JsonException("Could not parse the response stream."); }
        JsonElement root = doc.RootElement;

        string? title = root.GetProperty("title").GetString();
        string? imageUrl = null;
        try { imageUrl = root.GetProperty("thumbnail").GetProperty("source").GetString(); } catch { }
        string? summary = root.GetProperty("extract").GetString();

        if (root.GetProperty("type").GetString() == "disambiguation")
            summary = $"Could not find a wiki page with the name '{pageName}'. Was directed to disambiguation page.";

        doc.Dispose();

        return new WikiElement()
        {
            Title = title ?? "No Title Found",
            ImageUrl = imageUrl,
            Info = summary ?? "No Summary Found"
        };
    }

    public Task<WikiElement> GetInfoAsync(string query)
    {
        return GetSummary(query.FixURLWhitespace("_"));
    }
}
