using System;
using System.Collections.Generic;
using System.Text;

namespace Melodica.Jukebox.Models.Exceptions
{
    public class MediaUnavailableException : Exception
    {
        public MediaUnavailableException() : base("Media was unavailable.", null)
        { }
    }
}
