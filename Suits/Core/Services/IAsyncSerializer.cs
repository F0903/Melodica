using System.Threading.Tasks;

namespace Suits.Core.Services
{
    public interface IAsyncSerializer
    {
        public Task SerializeToFileAsync(string path, object toSerialize);

        public Task<T> DeserializeFileAsync<T>(string path);
    }
}