using Discord.Audio;
using Discord.Audio.Streams;
using System.Threading;
using System.Threading.Tasks;

namespace Melodica.Services.Audio;

/// <summary>
/// Wraps an audio stream to gracefully handle Dave encryption errors during initialization.
/// When Dave is not yet ready, audio passes through unencrypted without errors.
/// </summary>
public class DaveErrorHandlingStream(AudioOutStream innerStream) : AudioOutStream
{
    private readonly AudioOutStream _innerStream = innerStream;

    public override void WriteHeader(ushort seq, uint timestamp, bool missed)
    {
        _innerStream.WriteHeader(seq, timestamp, missed);
    }

    public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        try
        {
            await _innerStream.WriteAsync(buffer.AsMemory(offset, count), cancellationToken);
        }
        catch (Exception ex) when (ex.Message.Contains("MissingKeyRatchet") || ex.Message.Contains("encrypt"))
        {
            // Dave encryption not yet initialized, silently skip this frame
            // The audio will be lost for this frame but the stream won't crash
        }
    }

    public override Task FlushAsync(CancellationToken cancelToken)
        => _innerStream.FlushAsync(cancelToken);

    public override Task ClearAsync(CancellationToken cancelToken)
        => _innerStream.ClearAsync(cancelToken);

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _innerStream.Dispose();
        base.Dispose(disposing);
    }
}
