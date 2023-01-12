﻿namespace Melodica.Services.Audio;
internal class RawProcessor : IAsyncAudioProcessor
{
    internal RawProcessor(string file)
    {
        this.file = File.OpenRead(file);
    }

    readonly FileStream file;

    public ValueTask<Stream> ProcessAsync()
    {
        return ValueTask.FromResult((Stream)file);
    }

    public void Dispose()
    {
        file.Dispose();
    }
}