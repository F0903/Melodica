using System.Diagnostics;

namespace Melodica.Services.Audio;
internal class FFmpegProcessor : IAsyncAudioProcessor
{
    internal FFmpegProcessor(string input, string? inputFormat)
    {
        this.input = input;
        this.inputFormat = inputFormat;
    }

    readonly string input;
    readonly string? inputFormat;

    Process? proc;

    public void Dispose()
    {
        proc?.Dispose();
    }

    public ValueTask<Stream> ProcessAsync()
    {
        var isStream = inputFormat is not null && inputFormat == "hls";

        var inputFormatOption = inputFormat is not null ? $"-f {inputFormat}" : "";
        var formatSpecificInputOptions = !isStream ? "-flags +low_delay -fflags +discardcorrupt+fastseek+nobuffer -avioflags direct" : "";
        var formatSpecificOutputOptions = !isStream ? "-fflags +flush_packets" : "-bufsize 128k";
        var args = $"-y -hide_banner -loglevel panic -strict experimental -vn -protocol_whitelist pipe,file,http,https,tcp,tls,crypto {inputFormatOption} {formatSpecificInputOptions} -i {input} -f s16le {formatSpecificOutputOptions} -ac 2 -ar 48000 pipe:1";

        proc = new()
        {
            StartInfo = new ProcessStartInfo()
            {
                FileName = "ffmpeg",
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardError = false,
                RedirectStandardInput = input == "pipe:0" || input == "pipe:" || input == "-",
                RedirectStandardOutput = true,
                CreateNoWindow = true,
            }
        };

        proc.Start();

        return ValueTask.FromResult(proc.StandardOutput.BaseStream);
    }
}
