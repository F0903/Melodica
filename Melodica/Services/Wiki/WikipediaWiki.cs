using System.Net;
using System.Text.Json;
using System.Threading.Tasks;

using Melodica.Utility.Extensions;

namespace Melodica.Services.Wiki
{
    public class WikipediaWiki : WikiProvider
    {
        private const string wikipediaEndpoint = "https://en.wikipedia.org/api/rest_v1";
        private const string wikipediaSummaryEndpoint = wikipediaEndpoint + "/page/summary/";

        private WikiElement GetSummary(string pageName)
        {
            string? fullEndpoint = $"{wikipediaSummaryEndpoint}{pageName}?redirect=false";
            var request = WebRequest.CreateHttp(fullEndpoint);
            request.Method = "GET";

            var headers = request.Headers;
            headers.Add("accept: application/json; charset=utf-8");

            HttpWebResponse response;
            try { response = (HttpWebResponse)request.GetResponse(); }
            catch (WebException ex) when (ex.Status == WebExceptionStatus.ProtocolError)
            { throw new WebException($"Page was not found. Query was: \"{pageName}\"\nRemember that the name is case-sensitive."); }
            if (response == null) throw new WebException("Response from Wikipedia was null.");

            var status = response.StatusCode;
            if (status != HttpStatusCode.OK) throw new WebException($"Wikipedia returned code {status}");

            using var resStream = response.GetResponseStream();
            JsonDocument doc;
            try { doc = JsonDocument.Parse(resStream); }
            catch { throw new JsonException("Could not parse the response stream."); }
            var root = doc.RootElement;

            string? title = root.GetProperty("title").GetString();
            string? imageUrl = null;
            try { imageUrl = root.GetProperty("thumbnail").GetProperty("source").GetString(); } catch { }
            string? summary = root.GetProperty("extract").GetString();
            response.Close();
            doc.Dispose();

            return new WikiElement()
            {
                Title = title ?? "No Title Found",
                ImageUrl = imageUrl,
                Info = summary ?? "No Summary Found"
            };
        }

        public override Task<WikiElement> GetInfoAsync(string query) => Task.FromResult(GetSummary(query.FixURLWhitespace("_")));
    }
}