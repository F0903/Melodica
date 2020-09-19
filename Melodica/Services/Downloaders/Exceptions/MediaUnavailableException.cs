using System;
using System.Collections.Generic;
using System.Text;

namespace Melodica.Services.Downloaders.Exceptions
{
    public class MediaUnavailableException : DownloaderException
    {
        public MediaUnavailableException() : base("Media was unavailable.", null)
        { }
    }
}
