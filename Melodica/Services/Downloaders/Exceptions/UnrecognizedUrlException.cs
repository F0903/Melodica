namespace Melodica.Services.Downloaders.Exceptions;

public sealed class UnrecognizedUrlException : DownloaderException
{
    public UnrecognizedUrlException(string? msg = null, Exception? inner = null) : base(msg, inner)
    { }
}
