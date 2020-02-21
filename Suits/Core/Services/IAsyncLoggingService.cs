using System.Threading.Tasks;

namespace Suits.Core.Services
{
    public interface IAsyncLoggingService
    {
        public Task LogAsync(Discord.LogMessage msg);
    }
}