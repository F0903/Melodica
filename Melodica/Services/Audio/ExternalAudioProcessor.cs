using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

using Melodica.Core;
using Melodica.Core.Exceptions;
using Melodica.Services.Services;

using SpotifyAPI.Web;

namespace Melodica.Services.Audio
{
    public abstract class ExternalAudioProcessor : IDisposable
    {
        protected ExternalAudioProcessor(string mediaPath, int bufferSize = 1024, string? format = null)
        {
            this.processorProcess = ConstructExternal(mediaPath, bufferSize, format);
            this.processorProcess.Start();
            this.processorProcess.PriorityClass = BotSettings.ProcessPriority; 
        }

        ~ExternalAudioProcessor()
        {
            Dispose();
        }

        protected abstract Process ConstructExternal(string path, int bufferSize = 1024, string? format = null);

        protected readonly Process processorProcess;

        protected bool inputAvailable;
        protected bool outputAvailable;

        public Stream? GetInput() => inputAvailable ? processorProcess.StandardInput.BaseStream : null;

        public Stream? GetOutput() => outputAvailable ? processorProcess.StandardOutput.BaseStream : null;


        public Task DisposeAsync() => Task.Run(Dispose);

        public virtual void Dispose()
        {
            if (inputAvailable)
                processorProcess.StandardInput.Dispose();
            if (outputAvailable)
                processorProcess.StandardOutput.Dispose();

            if (processorProcess == null)
                return;
            processorProcess.Kill();
            if (!processorProcess.HasExited)
                processorProcess.WaitForExit();
        }
    }
}