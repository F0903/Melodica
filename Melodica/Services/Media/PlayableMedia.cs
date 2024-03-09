﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Melodica.Services.Audio;

namespace Melodica.Services.Media;

public class PlayableMedia(Stream data, MediaInfo info, PlayableMedia? next) : Stream
{
    protected readonly Stream data = data;

    public MediaInfo Info { get; set; } = info;

    public PlayableMedia? Next { get; set; } = next;

    IAsyncAudioProcessor? audioProcessor;

    public void AddAudioProcessor(IAsyncAudioProcessor audioProcessor)
    {
        this.audioProcessor = audioProcessor;
    }

    public override bool CanRead { get; } = true;
    public override bool CanSeek { get; } = false;
    public override bool CanWrite { get; } = false;
    public override long Length => data.Length;
    public override long Position { get => data.Position; set => data.Position = value; }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        var read = await data.ReadAsync(buffer, cancellationToken);
        if (audioProcessor is not null)
        {
            read = await audioProcessor.ProcessStreamAsync(buffer[..read]);
        }
        return read;
    }

    public override void Flush() => data.Flush();
    public override int Read(byte[] buffer, int offset, int count) => throw new NotImplementedException("Use async read.");
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    public override void Close() => data.Close();
}
