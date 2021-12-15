using System;

namespace Melodica.Services.Downloaders.Exceptions
{
    class UrlNotSupportedException : Exception
    {
        public UrlNotSupportedException(string? msg = null, Exception? inner = null) : base(msg, inner)
        { }
    }
}
