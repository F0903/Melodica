using Discord;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace Suits.Jukebox.Models
{
    public sealed class AudioProcessor : IDisposable
    {
        public AudioProcessor(string? path, int bufferSize = 1024, string? format = null)
        {
            playerProcess = Construct(path, bufferSize, format);
            playerProcess.Start();
            playerProcess.PriorityClass = ProcessPriorityClass.AboveNormal;
        }

        public AudioProcessor(string hlsUrl, int bufferSize = 1024)
        {
            playerProcess = Construct(hlsUrl, bufferSize, "hls");
            playerProcess.Start();
            playerProcess.PriorityClass = ProcessPriorityClass.AboveNormal;
            isLivestream = true;
        }

        ~AudioProcessor()
        {
            Dispose();
        }

        private Process Construct(string? path, int bufferSize = 1024, string? format = null)
        {
            if (path != null && path == string.Empty)
                throw new Exception("Song path is empty.");
            return new Process()
            {
                StartInfo = new ProcessStartInfo()
                {
                    FileName = "ffmpeg.exe",
                    Arguments = $"-y -hide_banner -loglevel debug -vn {(format != null ? $"-f {format}" : string.Empty)} -i {(path != null ? $"\"{path}\"" : "pipe:0")} -f s16le -bufsize {bufferSize} -ac 2 -ar 48000 pipe:1",
                    UseShellExecute = false,
                    RedirectStandardError = false,
                    RedirectStandardInput = (inputAvailable = (path == null ? true : false)),
                    RedirectStandardOutput = (outputAvailable = true),
                    CreateNoWindow = false,
                }
            };
        }

        private readonly Process playerProcess;

        private bool inputAvailable;
        private bool outputAvailable;

        public readonly bool isLivestream;

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