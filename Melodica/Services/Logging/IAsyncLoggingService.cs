using System.Threading.Tasks;

namespace Melodica.Services.Logging
{
    public interface IAsyncLoggingService
    {
        public Task LogAsync(Discord.LogMessage msg);
    }
}