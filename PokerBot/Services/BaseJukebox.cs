using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using PokerBot.Entities;

namespace PokerBot.Services
{
    public abstract class BaseJukebox
    {
        public static int Bitrate { get; protected set; } = 128 * 1024;

        public static int BufferSize { get; protected set; } = 64 * 1024;            

        protected static Player CreatePlayer(string file = null) // If file is null, use stdin
        {
            return new Player( new Process()
            {
                StartInfo = new ProcessStartInfo()
                {
                    FileName = "ffmpeg.exe",
                    Arguments = $"-hide_banner -loglevel debug -vn -i {file ?? "pipe:0"} -f s16le -bufsize {BufferSize} -filter:a dynaudnorm=b=1:c=1:n=0:r=0.2 -b:a {Bitrate} -ac 2 -ar 48000 -y pipe:1", //
                    UseShellExecute = false,
                    RedirectStandardError = false,
                    RedirectStandardOutput = true,
                    RedirectStandardInput = true,
                    CreateNoWindow = false,
                }
            });
        }
    }
}
