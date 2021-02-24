using System;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using AngleSharp;
using AngleSharp.Dom;

using Melodica.Core.Exceptions;
using Melodica.Utility.Extensions;

namespace Melodica.Services.Lyrics
{
    public class GeniusLyrics : ILyricsProvider
    {
        private static async Task<string> ParseLyricsAsync(string url)
        {
            var config = Configuration.Default.WithDefaultLoader();
            var context = BrowsingContext.New(config);

            IElement? lyricElement = null;
            int tries = 0;
            while (lyricElement == null)
            {
                if (tries > 5)
                    throw new CriticalException("GeniusLyrics exceeded max attempts. Could not get lyrics content from html.");

                using var doc = await context.OpenAsync(url);
                lyricElement = doc.QuerySelector("div.lyrics");

                Thread.Sleep(250 * tries);
                ++tries;
            }
            return lyricElement.TextContent;
        }

        private static async Task<LyricsInfo> SearchForSongAsync(string query)
        {
            string? fixedQuery = query.FixURLWhitespace();
            var req = WebRequest.CreateHttp($"https://api.genius.com/search?q={fixedQuery}");
            req.Headers = new WebHeaderCollection
            {
                $"Authorization:Bearer {Core.BotSecrets.GeniusAccessToken}"
            };
            req.Method = "GET";

            using var response = req.GetResponse();
            string? status = response.Headers.Get("Status");
            if (status == null || (status != null && status != "200 OK"))
                throw new HttpRequestException($"Server returned invalid status: {status}");

            var responseStream = response.GetResponseStream();
            using var fullResponse = await JsonDocument.ParseAsync(responseStream);

            var responseSection = fullResponse.RootElement.GetProperty("response");

            JsonElement GetElement(int index = 0)
            {
                if (index > 5) throw new Exception("Max attempts reached. Could not retrieve song info.");

                JsonElement hit;
                try { hit = responseSection.GetProperty("hits")[index]; }
                catch { throw new CriticalException($"No results found for lyrics search. Query was: {query}"); }
                var hitType = hit.GetProperty("type");
                if (hitType.GetString() != "song")
                    return GetElement(index++);
                else
                    return hit.GetProperty("result");
            }

            var songInfo = GetElement();
            string songTitle = songInfo.GetProperty("full_title").GetString() ?? throw new NullReferenceException("Could not convert JSON property to string.");
            string songImg = songInfo.GetProperty("header_image_url").GetString() ?? throw new NullReferenceException("Could not convert JSON property to string.");
            string songLyrics = await ParseLyricsAsync(songInfo.GetProperty("url").GetString() ?? throw new NullReferenceException("Could not convert JSON property to string."));

            return new LyricsInfo { Image = songImg, Title = songTitle, Lyrics = songLyrics };
        }

        public async Task<LyricsInfo> GetLyricsAsync(string query)
        {
            var lyrics = await SearchForSongAsync(query);
            return lyrics;
        }
    }
}