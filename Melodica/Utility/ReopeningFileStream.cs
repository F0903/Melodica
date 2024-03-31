namespace Melodica.Utility;
internal class ReopeningFileStream(string path, FileMode mode, FileAccess access, FileShare share) : Stream
{
    FileStream? baseStream;

    public override bool CanRead => baseStream is not null && baseStream!.CanRead;

    public override bool CanSeek => baseStream is not null && baseStream!.CanSeek;

    public override bool CanWrite => baseStream is not null && baseStream!.CanWrite;

    public override long Length => baseStream is not null ? baseStream!.Length : 0;

    public override long Position { get => baseStream is not null ? baseStream!.Position : 0; set { if (baseStream is not null) baseStream!.Position = value; } }

    void Reopen()
    {
        baseStream = File.Open(path, mode, access, share);
    }

    public override void Flush()
    {
        if (baseStream is null)
        {
            Reopen();
            Flush();
            return;
        }
        baseStream.Flush();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        if (baseStream is null)
        {
            Reopen();
            return Read(buffer, offset, count);
        }
        return baseStream.Read(buffer, offset, count);
    }

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (baseStream is null)
        {
            Reopen();
            return ReadAsync(buffer, cancellationToken);
        }
        return baseStream.ReadAsync(buffer, cancellationToken);
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        if (baseStream is null)
        {
            Reopen();
            return Seek(offset, origin);
        }
        return baseStream.Seek(offset, origin);
    }

    public override void SetLength(long value)
    {
        if (baseStream is null)
        {
            Reopen();
            SetLength(value);
            return;
        }
        baseStream.SetLength(value);
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        if (baseStream is null)
        {
            Reopen();
            Write(buffer, offset, count);
            return;
        }
        baseStream.Write(buffer, offset, count);
    }

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (baseStream is null)
        {
            Reopen();
            return WriteAsync(buffer, cancellationToken);
        }
        return baseStream.WriteAsync(buffer, cancellationToken);
    }

    protected override void Dispose(bool disposing)
    {
        if (baseStream is null) return;
        baseStream.Dispose();
        baseStream = null;
    }

    public override async ValueTask DisposeAsync()
    {
        if (baseStream is null) return;
        await baseStream.DisposeAsync();
    }

    public override void Close()
    {
        if (baseStream is null) return;
        baseStream.Close();
    }
}
