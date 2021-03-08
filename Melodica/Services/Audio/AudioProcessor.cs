using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

using Melodica.Services.Media;

namespace Melodica.Services.Audio
{
    public abstract class AudioProcessor : IDisposable
    {
        protected AudioProcessor(bool input, bool output)
        {
            inputAvailable = input;
            outputAvailable = output;
        }

        protected readonly bool inputAvailable;
        protected readonly bool outputAvailable;

        private Process? processorProcess;
        private bool disposedValue;

        public Stream? GetInput() => inputAvailable ? processorProcess?.StandardInput.BaseStream : null;

        public Stream? GetOutput() => outputAvailable ? processorProcess?.StandardOutput.BaseStream : null;

        protected abstract Task<Process> CreateAsync(PlayableMedia media, TimeSpan? startingPoint = null);

        public ValueTask Process(PlayableMedia media, TimeSpan? startingPoint = null)
        {
            return new ValueTask(Task.Run(async () => (processorProcess = await CreateAsync(media, startingPoint)).Start()));
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    if (processorProcess is null)
                        return;
                    processorProcess.Kill();

                    if (inputAvailable)
                        processorProcess.StandardInput.Dispose();
                    if (outputAvailable)
                        processorProcess.StandardOutput.Dispose();
                }

                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}