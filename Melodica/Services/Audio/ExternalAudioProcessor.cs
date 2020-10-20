using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

using Melodica.Core;

namespace Melodica.Services.Audio
{
    public abstract class ExternalAudioProcessor : IDisposable
    {
        protected ExternalAudioProcessor(string mediaPath, string? format = null, TimeSpan? startingPoint = null)
        {
            processorProcess = ConstructExternal(mediaPath, format, startingPoint);
            processorProcess.Start();
            processorProcess.PriorityClass = BotSettings.ProcessPriority;
        }

        ~ExternalAudioProcessor()
        {
            Dispose();
        }

        protected abstract Process ConstructExternal(string path, string? format = null, TimeSpan? startingPoint = null);

        protected readonly Process processorProcess;

        protected bool inputAvailable;
        protected bool outputAvailable;

        public Stream? GetInput() => inputAvailable ? processorProcess.StandardInput.BaseStream : null;

        public Stream? GetOutput() => outputAvailable ? processorProcess.StandardOutput.BaseStream : null;


        public Task DisposeAsync() => Task.Run(Dispose);

        public virtual void Dispose()
        {
            if (processorProcess == null)
                return;
            processorProcess.CloseMainWindow();
            
            if (inputAvailable)
                processorProcess.StandardInput.Dispose();
            if (outputAvailable)
                processorProcess.StandardOutput.Dispose();

            if (!processorProcess.HasExited)
                if (!processorProcess.WaitForExit(1000))
                    processorProcess.Kill();
        }
    }
}