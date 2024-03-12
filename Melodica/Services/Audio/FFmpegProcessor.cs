using System.Diagnostics;
using System.Reflection.Metadata.Ecma335;
using Melodica.Services.Caching;
using Melodica.Services.Media;

namespace Melodica.Services.Audio;

public class FFmpegProcessor : IAsyncMediaProcessor
{
    Process? proc;

    Stream? processInput;
    Stream? processOutput;

    readonly ManualResetEvent pauseWaiter = new(true);
    bool paused = false;

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

    Task StartProcess(string? explicitDataFormat)
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

    public async Task ProcessMediaAsync(PlayableMediaStream media, Stream output, Action? beforeInterruptionCallback, CancellationToken token)
    {
        if (proc is null || proc.HasExited)
        {
            var info = await media.GetInfoAsync();
            await StartProcess(info.ExplicitDataFormat);
        }

        var tokenCallback = token.Register(() => SetPause(false)); // Make sure we are not blocking by waiting when requesting cancel.

        void HandlePause()
        {
            if (!paused) return;
            beforeInterruptionCallback?.Invoke();
            pauseWaiter.WaitOne();
        }

        const int bufferSize = 8 * 1024;

        try
        {
            var inputTask = Task.Run(async () =>
            {
                int read = 0;
                Memory<byte> buf = new byte[bufferSize];
                try
                {
                    while ((read = await media.ReadAsync(buf, token)) != 0)
                    {
                        HandlePause();
                        await processInput!.WriteAsync(buf[..read], token);
                    }
                } 
                finally
                {
                    await processInput!.FlushAsync(token);
                    processInput!.Close();
                }
                
            }, token);

            var outputTask = Task.Run(async () =>
            {
                int read = 0;
                Memory<byte> buf = new byte[bufferSize];
                while ((read = await processOutput!.ReadAsync(buf, token)) != 0)
                {
                    HandlePause();
                    await output.WriteAsync(buf[..read], token);
                }
            }, token);

            await Task.WhenAll(inputTask, outputTask);
        }
        finally
        {
            tokenCallback.Unregister();
            proc!.Kill();
        }
    }
}
