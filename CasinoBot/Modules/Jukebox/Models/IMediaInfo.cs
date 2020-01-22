using System;
using System.Collections.Generic;
using System.Text;

namespace CasinoBot.Modules.Jukebox.Models
{
    public interface IMediaInfo
    {
        public string GetTitle();
        public TimeSpan GetDuration();
    }
}
