using System.Diagnostics;

namespace Melodica.Services.Audio;

public class FFmpegProcessor : IAsyncAudioProcessor
{
    Process? proc;

    Stream? processInput;
    Stream? processOutput;

    readonly ManualResetEvent pauseWaiter = new(true);
    bool paused = false;

    bool gracefulStopRequested = false;

    public void Dispose()
    {
        proc?.Dispose();
        proc = null;
        processInput?.Dispose();
        processInput = null;
        processOutput?.Dispose();
        processOutput = null;
        pauseWaiter.Dispose();
        GC.SuppressFinalize(this);
    }

    Task StartProcess(string? explicitDataFormat = null)
    {
        var args = $"-nostdin -y -hide_banner -loglevel debug -strict experimental -vn -protocol_whitelist pipe,file,http,https,tcp,tls,crypto {(explicitDataFormat is not null ? $"-f {explicitDataFormat}" : "")} -i pipe: -f s16le -ac 2 -ar 48000 pipe:";

        proc = new()
        {
            StartInfo = new ProcessStartInfo()
            {
                FileName = "ffmpeg",
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                CreateNoWindow = true,
            }
        };

        proc.Start();

        //DEBUGGING
        new Thread(() =>
        {
            Span<char> buf = stackalloc char[256];
            while (!proc.HasExited && proc is not null)
            {
                var read = proc.StandardError.Read(buf);
                if (read == 0) continue;
                Console.WriteLine(buf[..read].ToString());
            }
        }).Start();

        processInput = proc.StandardInput.BaseStream;
        processOutput = proc.StandardOutput.BaseStream;

        return Task.CompletedTask;
    }

    public void SetPause(bool value)
    {
        paused = value;
        if (value) pauseWaiter.Reset();
        else pauseWaiter.Set();
    }

    public void StopRequested()
    {
        gracefulStopRequested = true;
    }

    public async Task ProcessStreamAsync(Stream input, Stream output, Action? beforeInterruptionCallback, CancellationToken token, string? explicitDataFormat = null)
    {
        if (proc is null || proc.HasExited)
        {
            await StartProcess(explicitDataFormat);
        }
        
        token.Register(() => pauseWaiter.Set()); // Make sure we are not blocking by waiting when requesting cancel.

        void HandlePause()
        {
            if (!paused) return;
            beforeInterruptionCallback?.Invoke();
            pauseWaiter.WaitOne();
        }

        const int bufferSize = 8 * 1024;

        var readTask = Task.Run(async () =>
        {
            int read = 0;
            Memory<byte> buf = new byte[bufferSize];
            while ((read = await input.ReadAsync(buf, token)) != 0)
            {
                if(gracefulStopRequested) return;
                HandlePause();
                await processInput!.WriteAsync(buf[..read], token);
            }
            await processInput!.FlushAsync(token);
        }, token);

        var writeTask = Task.Run(async () =>
        {
            int read = 0;
            Memory<byte> buf = new byte[bufferSize];
            while (!readTask.IsCompleted && (read = await processOutput!.ReadAsync(buf)) != 0)
            {
                if (gracefulStopRequested) return;
                HandlePause();
                await output.WriteAsync(buf[..read], token);
            }
            await output.FlushAsync(token);
        }, token);

        await Task.WhenAny(readTask, writeTask).ContinueWith(_ => {
            proc!.Kill();
            gracefulStopRequested = false;
        }, token);
    }
}
