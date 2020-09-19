using System;
using System.Collections.Generic;
using System.Text;

namespace Melodica.Services.Downloaders.Exceptions
{
    public class DownloaderException : Exception
    {
        public DownloaderException(string? msg = null, Exception? inner = null) : base(msg, inner) { }
    }
}
