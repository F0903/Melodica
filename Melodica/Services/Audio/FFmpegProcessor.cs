using System.Diagnostics;

namespace Melodica.Services.Audio;

public class FFmpegProcessor : IAsyncAudioProcessor
{
    Process? proc;

    Stream? inputStream;
    Stream? outputStream;

    public void Dispose()
    {
        proc?.Dispose();
        GC.SuppressFinalize(this);
    }

    void StartProcess()
    {
        var args = $"-y -hide_banner -loglevel debug -strict experimental -vn -protocol_whitelist pipe,file,http,https,tcp,tls,crypto -i pipe:0 -f s16le -ac 2 -ar 48000 pipe:1";

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
            Span<char> buf = new char[512];
            var read = proc.StandardError.Read(buf);
            Console.WriteLine(buf[..read].ToString());
        }).Start();

        inputStream = proc.StandardInput.BaseStream;
        outputStream = proc.StandardOutput.BaseStream;
    }

    //TODO: just pass the damn url and let ffmpeg do the downloading and stream/cache through std out
    public async ValueTask<int> ProcessStreamAsync(Memory<byte> memory)
    {
        if (proc is null) StartProcess();

        await inputStream!.WriteAsync(memory);
        await inputStream.FlushAsync();
        var read = await outputStream!.ReadAsync(memory);
        return read;
    }
}
