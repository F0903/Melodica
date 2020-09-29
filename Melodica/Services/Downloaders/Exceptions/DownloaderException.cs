using System;

namespace Melodica.Services.Downloaders.Exceptions
{
    public class DownloaderException : Exception
    {
        public DownloaderException(string? msg = null, Exception? inner = null) : base(msg, inner) { }
    }
}
