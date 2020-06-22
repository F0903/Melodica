using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace Suits
{
    public static class GlobalTimer
    {
        private static readonly Stopwatch sw = new Stopwatch();

        public static void Start() => sw.Start();

        public static void Stop() => sw.Stop();

        public static void Reset() => sw.Reset();

        public static TimeSpan GetTime() => sw.Elapsed;
    }
}
