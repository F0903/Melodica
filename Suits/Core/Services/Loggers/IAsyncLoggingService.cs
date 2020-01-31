using System.Threading.Tasks;

namespace Suits.Core.Services.Loggers
{
    public interface IAsyncLoggingService
    {
        public Task LogAsync(Discord.LogMessage msg);
    }
}