using System.Threading.Tasks;

namespace CasinoBot.Services.Loggers
{
    public interface IAsyncLoggingService
    {
        public Task LogAsync(Discord.LogMessage msg);
    }
}