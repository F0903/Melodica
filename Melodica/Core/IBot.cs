using System.Threading.Tasks;

namespace Melodica.Core
{
    public interface IBot
    {
        public Task ConnectAsync(bool startOnConnect = false);

        public Task StartAsync();

        public Task StopAsync();
    }
}