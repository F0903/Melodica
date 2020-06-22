using System;
using System.Collections.Generic;
using System.Text;

namespace Suits.Jukebox.Models.Exceptions
{
    public class CriticalException : Exception
    {
        public CriticalException(string? msg = null, Exception? innerEx = null) : base(msg, innerEx)
        {

        }
    }
}
