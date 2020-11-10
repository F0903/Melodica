using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace Melodica.Services.Playback
{
    public class PlaybackStopwatch : Stopwatch
    {
        TimeSpan offset;

        public new TimeSpan Elapsed 
        { 
            get => offset + base.Elapsed;
            set => offset = value;
        }

        public TimeSpan LastDuration { get; private set; }

        public new void Reset()
        {
            LastDuration = Elapsed;
            base.Reset();
            offset = TimeSpan.Zero;
        }

        public new void Stop()
        {
            LastDuration = Elapsed;
            base.Stop();
        }
    }
}
