using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Melodica.Services.Downloaders.Exceptions
{
    class UrlNotSupportedException : Exception
    {
        public UrlNotSupportedException(string? msg = null, Exception? inner = null) : base(msg, inner)
        { }
    }
}
