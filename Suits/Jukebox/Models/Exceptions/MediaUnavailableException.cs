using System;
using System.Collections.Generic;
using System.Text;

namespace Suits.Jukebox.Models.Exceptions
{
    public class MediaUnavailableException : Exception
    {
        public MediaUnavailableException() : base("Media was unavailable.", null)
        { }
    }
}
