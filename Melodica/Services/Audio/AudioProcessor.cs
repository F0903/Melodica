using System.Diagnostics;

using Melodica.Services.Media;

namespace Melodica.Services.Audio;

public abstract class AudioProcessor : IDisposable
{
    protected AudioProcessor(bool inputPipe, bool outputPipe)
    {
        this.inputPipe = inputPipe;
        this.outputPipe = outputPipe;
    }

    protected readonly bool inputPipe;
    protected readonly bool outputPipe;

    private Process? proc;
    private bool disposedValue;

    public Stream? GetInput()
    {
        return inputPipe ? proc?.StandardInput.BaseStream : null;
    }

    public Stream? GetOutput()
    {
        return outputPipe ? proc?.StandardOutput.BaseStream : null;
    } 

    public async ValueTask StartProcess(DataInfo dataInfo, TimeSpan? startingPoint = null)
    {
        proc = await CreateAsync(dataInfo, startingPoint);
        proc.Start();
    }

    public Task WaitForExit(CancellationToken token = default)
    {
        return proc is null ? Task.CompletedTask : proc.WaitForExitAsync(token);
    }


    protected abstract Task<Process> CreateAsync(DataInfo dataInfo, TimeSpan? startingPoint = null);

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                if (proc is null)
                    return;
                proc.Kill();

                if (inputPipe)
                    proc.StandardInput.Dispose();
                if (outputPipe)
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
}
