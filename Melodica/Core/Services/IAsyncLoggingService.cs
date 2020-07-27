using System.Threading.Tasks;

namespace Melodica.Core.Services
{
    public interface IAsyncLoggingService
    {
        public Task LogAsync(Discord.LogMessage msg);
    }
}