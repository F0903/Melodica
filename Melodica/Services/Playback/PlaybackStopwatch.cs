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
            set => offset += value;
        }

        public TimeSpan LastDuration { get; private set; }

        public new void Reset()
        {
            base.Reset();
            LastDuration = Elapsed;
        }

        public new void Stop()
        {
            base.Stop();
            LastDuration = Elapsed;
        }
    }
}
