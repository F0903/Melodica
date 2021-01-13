using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace Melodica.Services.Audio
{
    public abstract class AudioProcessor
    {
        ~AudioProcessor()
        {
            if (processorProcess == null)
                return;
            processorProcess.Kill();

            if (inputAvailable)
                processorProcess.StandardInput.Dispose();
            if (outputAvailable)
                processorProcess.StandardOutput.Dispose();
        }

        protected AudioProcessor(bool input, bool output)
        {
            inputAvailable = input;
            outputAvailable = output;
        }

        protected readonly bool inputAvailable;
        protected readonly bool outputAvailable;

        private Process? processorProcess;

        public Stream? GetInput() => inputAvailable ? processorProcess?.StandardInput.BaseStream : null;

        public Stream? GetOutput() => outputAvailable ? processorProcess?.StandardOutput.BaseStream : null;

        protected abstract Task<Process> ConstructAsync(string path, string? format, TimeSpan? startingPoint = null);

        public ValueTask Process(string path, string? format, TimeSpan? startingPoint = null)
        {
            return new ValueTask(Task.Run(async () => (processorProcess = await ConstructAsync(path, format, startingPoint)).Start()));
        }
    }
}