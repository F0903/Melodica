using System.Buffers;
using System.Diagnostics;
using Melodica.Services.Media;

namespace Melodica.Services.Audio;

public class FFmpegProcessor : IAsyncMediaProcessor
{
    readonly static MemoryPool<byte> memory = MemoryPool<byte>.Shared;

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

    Task StartProcessAsync(string? explicitDataFormat)
    {
        //TODO: remove debug logging
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

        processInput = proc.StandardInput.BaseStream;
        processOutput = proc.StandardOutput.BaseStream;

        //TODO: remove debug logging
        //DEBUGGING
        Task.Run(() =>
        {
            var err = proc.StandardError;
            while (true)
            {
                var line = err.ReadLine();
                if (line is null) break;
                Console.WriteLine(line);
            }
        });

        return Task.CompletedTask;
    }

    public void SetPause(bool value)
    {
        paused = value;
        if (value) pauseWaiter.Reset();
        else pauseWaiter.Set();
    }

    public async Task ProcessMediaAsync(PlayableMedia media, Stream output, Action? onHalt, Action? onResume, int bufferSize = 3840, CancellationToken cancellationToken = default)
    {
        if (proc is null || proc.HasExited)
        {
            var info = await media.GetInfoAsync(cancellationToken);
            await StartProcessAsync(info.ExplicitDataFormat);
        }

        var tokenCallback = cancellationToken.Register(() => SetPause(false)); // Make sure we are not blocking by waiting when requesting cancel.

        void HandlePause()
        {
            if (!paused) return;
            onHalt?.Invoke();
            pauseWaiter.WaitOne();
            onResume?.Invoke();
        }

        try
        {
            var inputTask = Task.Run(async () =>
            {
                int read = 0;
                using var mem = memory.Rent(bufferSize);
                var buf = mem.Memory;
                try
                {
                    while ((read = await media.ReadAsync(buf, cancellationToken)) != 0)
                    {
                        HandlePause();
                        await processInput!.WriteAsync(buf[..read], cancellationToken);
                    }
                }
                finally
                {
                    await processInput!.FlushAsync(cancellationToken);
                    processInput!.Close();
                }

            }, cancellationToken);

            var outputTask = Task.Run(async () =>
            {
                int read = 0;
                using var mem = memory.Rent(bufferSize);
                var buf = mem.Memory;
                while ((read = await processOutput!.ReadAsync(buf, cancellationToken)) != 0)
                {
                    HandlePause();
                    await output.WriteAsync(buf[..read], cancellationToken);
                }
            }, cancellationToken);

            await Task.WhenAll(inputTask, outputTask);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Caught error in FFmpegProcessor!\n{ex}");
        }
        finally
        {
            tokenCallback.Unregister();
            proc!.Close();
            proc = null;
        }
    }
}
