using System.Threading.Tasks;

namespace Melodica.Core
{
    public interface IAsyncBot
    {
        public Task ConnectAsync(bool startOnConnect = false);

        public Task StartAsync();

        public Task StopAsync();
    }
}