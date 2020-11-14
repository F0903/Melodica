using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading.Tasks;

namespace Melodica.Services.Serialization
{
    public class BinarySerializer : IAsyncSerializer
    {
        private static readonly BinaryFormatter bin = new BinaryFormatter(null, new StreamingContext(StreamingContextStates.File | StreamingContextStates.CrossProcess | StreamingContextStates.Persistence));

        //TODO: Convert this to use JSON to stop the screaming warning messages...

        public Task SerializeToFileAsync(string path, object toSerialize)
        {
            using var file = new FileStream(path, FileMode.OpenOrCreate, FileAccess.Write, FileShare.Write);
            bin.Serialize(file, toSerialize);
            return Task.CompletedTask;
        }

        public Task<T> DeserializeFileAsync<T>(string path)
        {
            using var file = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            return Task.FromResult((T)Convert.ChangeType(bin.Deserialize(file), typeof(T)));
        }
    }
}