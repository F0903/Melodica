using System;
using System.Collections.Generic;
using System.Text;

namespace Melodica.Services.Playback.Exceptions
{
    public class JukeboxException : Exception
    {
        public JukeboxException(string? msg = null, Exception? inner = null) : base(msg, inner)
        { }
    }
}
