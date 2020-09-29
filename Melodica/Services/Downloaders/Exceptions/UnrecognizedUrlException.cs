using System;
using System.Collections.Generic;
using System.Text;

namespace Melodica.Services.Downloaders.Exceptions
{
    public class UnrecognizedUrlException : DownloaderException
    {
        public UnrecognizedUrlException(string? msg = null, Exception? inner = null) : base(msg, inner) 
        { }
    }
}
