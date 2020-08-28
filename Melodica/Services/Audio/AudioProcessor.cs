using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

using Melodica.Core;
using Melodica.Core.Exceptions;
using Melodica.Services.Services;

namespace Melodica.Services.Audio
{
    public class AudioProcessor : IDisposable
    {
        public AudioProcessor(string path, int bufferSize = 1024, string? format = null)
        {
            playerProcess = ConstructExternal(path, bufferSize, format);
            playerProcess.Start();
            playerProcess.PriorityClass = BotSettings.ProcessPriority;
        }

        ~AudioProcessor()
        {
            Dispose();
        }

        protected virtual Process ConstructExternal(string path, int bufferSize = 1024, string? format = null)
        {
            if (path == null || path == string.Empty)
            {
                MediaCache.PruneAllCachesAsync().Wait();
                throw new CriticalException("Song path is empty... Clearing cache... (something went wrong here)");
            }
            return new Process()
            {
                StartInfo = new ProcessStartInfo()
                {
                    FileName = "ffmpeg.exe",
                    Arguments = $"-y -hide_banner -loglevel debug -vn {(format != null ? $"-f {format}" : string.Empty)} -i {(path != null ? $"\"{path}\"" : "pipe:0")} -f s16le -bufsize {bufferSize} -ac 2 -ar 48000 pipe:1",
                    UseShellExecute = false,
                    RedirectStandardError = false,
                    RedirectStandardInput = (inputAvailable = (path == null)),
                    RedirectStandardOutput = (outputAvailable = true),
                    CreateNoWindow = false,
                }
            };
        }

        private readonly Process playerProcess;

        protected bool inputAvailable;
        protected bool outputAvailable;

        public virtual Process GetBaseProcess() => playerProcess;

        public Stream? GetInput() => inputAvailable ? playerProcess.StandardInput.BaseStream : null;

        public Stream? GetOutput() => outputAvailable ? playerProcess.StandardOutput.BaseStream : null;

        public void Stop() => Dispose();

        public Task DisposeAsync() => Task.Run(Dispose);

        public virtual void Dispose()
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