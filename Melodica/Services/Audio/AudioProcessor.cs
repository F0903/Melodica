using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

using Melodica.Core;
using Melodica.Core.Exceptions;
using Melodica.Services.Services;

namespace Melodica.Services.Audio
{
    public abstract class AudioProcessor : IDisposable
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

        protected abstract Process ConstructExternal(string path, int bufferSize = 1024, string? format = null);

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