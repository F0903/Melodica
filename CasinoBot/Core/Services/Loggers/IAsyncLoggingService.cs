using System.Threading.Tasks;

namespace CasinoBot.Core.Services.Loggers
{
    public interface IAsyncLoggingService
    {
        public Task LogAsync(Discord.LogMessage msg);
    }
}