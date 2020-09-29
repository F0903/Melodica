using System.Threading.Tasks;

namespace Melodica.Services.Wiki
{
    public struct WikiElement
    {
        public string Title { get; set; }
        public string Info { get; set; }
        public string? ImageUrl { get; set; }
    }

    public abstract class WikiProvider
    {
        public abstract Task<WikiElement> GetInfoAsync(string query);
    }
}
