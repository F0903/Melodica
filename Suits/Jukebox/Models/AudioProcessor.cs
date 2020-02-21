using Discord;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

#nullable enable

namespace Suits.Jukebox.Models
{
    public sealed class AudioProcessor : IDisposable
    {
        public AudioProcessor(string? path, int bitrate, int bufferSize, string? format = null)
        {
            if (path != null && path == string.Empty)
                throw new Exception("Song path is empty.");
            playerProcess = new Process()
            {
                StartInfo = new ProcessStartInfo()
                {
                    FileName = Environment.Is64BitProcess ? "ffmpeg64.exe" : "ffmpeg32.exe",
                    Arguments = $"-hide_banner -loglevel debug -vn {(format != null ? $"-f {format}" : string.Empty)} -i {$"\"{path}\"" ?? "pipe:0"} -f s16le -bufsize {bufferSize} -b:a {bitrate} -ac 2 -ar 48000 -y pipe:1",
                    UseShellExecute = false,
                    RedirectStandardError = false,
                    RedirectStandardInput = (inputAvailable = (path == null ? true : false)),
                    RedirectStandardOutput = (outputAvailable = true),
                    CreateNoWindow = false,
                }
            };

            playerProcess.Start();
        }

        private readonly Process playerProcess;

        private readonly bool inputAvailable;
        private readonly bool outputAvailable;

        public Process GetBaseProcess() => playerProcess;

        public Stream? GetInput() => inputAvailable ? playerProcess.StandardInput.BaseStream : null;

        public Stream? GetOutput() => outputAvailable ? playerProcess.StandardOutput.BaseStream : null;

        public void Stop() => Dispose();

        public Task DisposeAsync() => Task.Run(Dispose);

        public void Dispose()
        {
            if (inputAvailable)
                playerProcess.StandardInput.Dispose();
            if (outputAvailable)
                playerProcess.StandardOutput.Dispose();

            playerProcess.Kill();
            if (!playerProcess.HasExited)
                playerProcess.WaitForExit();
        }
    }
}