namespace Melodica.Services.Downloaders.Exceptions;

public sealed class MissingMetadataException : DownloaderException
{
    public MissingMetadataException(string? msg = null, Exception? inner = null) : base(msg, inner)
    {
    }
}
