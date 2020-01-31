using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading.Tasks;

namespace Suits.Jukebox.Services
{
    public static class Serializer
    {
        private static readonly BinaryFormatter bin = new BinaryFormatter(null, new StreamingContext(StreamingContextStates.File | StreamingContextStates.CrossProcess | StreamingContextStates.Persistence));

        public static Task SerializeToFileAsync(string path, object toSerialize)
        {
            using var file = new FileStream(path, FileMode.OpenOrCreate);
            bin.Serialize(file, toSerialize);
            return Task.CompletedTask;
        }

        public static Task<T> DeserializeFileAsync<T>(string path)
        {
            using var file = File.Open(path, FileMode.Open);
            return Task.FromResult((T)Convert.ChangeType(bin.Deserialize(file), typeof(T)));
        }
    }
}
