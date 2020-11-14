using System;

namespace Melodica.Services.Downloaders.Exceptions
{
    public class MediaUnavailableException : DownloaderException
    {
        public MediaUnavailableException(string? msg = "Media was unavailable.", Exception? inner = null) : base(msg, inner)
        { }
    }
}