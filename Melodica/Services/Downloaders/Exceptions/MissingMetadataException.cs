using System;

namespace Melodica.Services.Downloaders.Exceptions
{
    public class MissingMetadataException : DownloaderException
    {
        public MissingMetadataException(string? msg = null, Exception? inner = null) : base(msg, inner) { }
    }
}
