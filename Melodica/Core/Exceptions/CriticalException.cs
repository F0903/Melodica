using System;

namespace Melodica.Core.Exceptions
{
    public class CriticalException : Exception
    {
        public CriticalException(string? msg = null, Exception? innerEx = null) : base(msg, innerEx)
        {
        }
    }
}