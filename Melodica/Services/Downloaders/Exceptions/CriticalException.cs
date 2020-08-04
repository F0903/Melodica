using System;
using System.Collections.Generic;
using System.Text;

namespace Melodica.Services.Downloaders.Exceptions
{
    public class CriticalException : Exception
    {
        public CriticalException(string? msg = null, Exception? innerEx = null) : base(msg, innerEx)
        {

        }
    }
}
