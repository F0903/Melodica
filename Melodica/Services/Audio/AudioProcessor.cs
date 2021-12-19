using System.Diagnostics;

using Melodica.Services.Media;

namespace Melodica.Services.Audio;

public abstract class AudioProcessor : IDisposable
{
    protected AudioProcessor(bool input, bool output)
    {
        inputAvailable = input;
        outputAvailable = output;
    }

    protected readonly bool inputAvailable;
    protected readonly bool outputAvailable;

    private Process? proc;
    private bool disposedValue;

    public Stream? GetInput()
    {
        return inputAvailable ? proc?.StandardInput.BaseStream : null;
    }

    public Stream? GetOutput()
    {
        return outputAvailable ? proc?.StandardOutput.BaseStream : null;
    }

    protected abstract Task<Process> CreateAsync(DataInfo dataInfo, TimeSpan? startingPoint = null);

    public async ValueTask StartProcess(DataInfo dataInfo, TimeSpan? startingPoint = null)
    {
        proc = await CreateAsync(dataInfo, startingPoint);
        proc.Start();
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                if (proc is null)
                    return;
                proc.Kill();

                if (inputAvailable)
                    proc.StandardInput.Dispose();
                if (outputAvailable)
                    proc.StandardOutput.Dispose();
            }

            disposedValue = true;
        }
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    public Task WaitForExit(CancellationToken token = default)
    {
        return proc is null ? Task.CompletedTask : proc.WaitForExitAsync(token);
    }
}
