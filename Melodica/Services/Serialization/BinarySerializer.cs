using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading.Tasks;

namespace Melodica.Services.Serialization
{
    public class BinarySerializer : IAsyncSerializer
    {
        private static readonly BinaryFormatter bin = new BinaryFormatter(null, new StreamingContext(StreamingContextStates.File | StreamingContextStates.CrossProcess | StreamingContextStates.Persistence));

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
