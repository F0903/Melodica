namespace Melodica.Services.Downloaders.Exceptions;

public sealed class MediaUnavailableException : DownloaderException
{
    public MediaUnavailableException(string? msg = "Media was unavailable.", Exception? inner = null) : base(msg, inner)
    { }
}
