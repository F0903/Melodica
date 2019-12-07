using Discord;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

#nullable enable
namespace PokerBot.Entities
{
    public sealed class AudioProcessor : IDisposable
    {
        public AudioProcessor(string? path, int bitrate, int bufferSize, string? format = null)
        {
            playerProcess = new Process()
            {
                StartInfo = new ProcessStartInfo()
                {
                    FileName = "ffmpeg.exe",
                    Arguments = $"-hide_banner -loglevel debug -vn {(format != null ? $"-f {format}" : string.Empty)} -i {path ?? "pipe:0"} -f s16le -bufsize {bufferSize} -b:a {bitrate} -ac 2 -ar 48000 -y pipe:1", // -filter:a dynaudnorm=b=1:c=1:n=0:r=0.2
                    UseShellExecute = false,
                    RedirectStandardError = false,
                    RedirectStandardOutput = true,
                    RedirectStandardInput = path == null ? true : false,
                    CreateNoWindow = Core.Settings.LogSeverity == LogSeverity.Debug ? false : true,
                }
            };

            playerProcess.Start();
        }

        private readonly Process playerProcess;

        public Process GetBaseProcess() => playerProcess;

        public Stream GetOutput() => playerProcess.StandardOutput.BaseStream;
        public Stream GetInput() => playerProcess.StandardInput.BaseStream;

        public void Stop() => Dispose();

        public void Dispose()
        {
            playerProcess.Kill();
            playerProcess.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
