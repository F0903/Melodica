﻿using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;

namespace Melodica.Services.Serialization;

public class BinarySerializer : IAsyncSerializer
{
    // Disable redundant warning. These files are only stored locally and contain no sensitive info.
#pragma warning disable SYSLIB0011

    private static readonly BinaryFormatter bin = new(null, new StreamingContext(StreamingContextStates.File | StreamingContextStates.Persistence));

    public Task SerializeToFileAsync(string path, object toSerialize)
    {
        using FileStream? file = new FileStream(path, FileMode.OpenOrCreate, FileAccess.Write, FileShare.Write);


        bin.Serialize(file, toSerialize);


        return Task.CompletedTask;
    }

    public Task<T> DeserializeFileAsync<T>(string path)
    {
        using FileStream? file = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Task.FromResult((T)bin.Deserialize(file));
    }

#pragma warning restore SYSLIB0011
}
