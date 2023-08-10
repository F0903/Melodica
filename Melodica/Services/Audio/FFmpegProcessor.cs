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

    public Task<ProcessorStreams> ProcessAsync()
    {
        var isStream = inputFormat is not null and "hls";
        var isInputPiped = input == "pipe:0" || input == "pipe:" || input == "-";

        var inputFormatOption = inputFormat is not null ? $"-f {inputFormat}" : "";
        var formatSpecificInputOptions = !isStream ? "-flags +low_delay -fflags +discardcorrupt+fastseek+nobuffer -avioflags direct" : "";
        var formatSpecificOutputOptions = !isStream ? "-fflags +flush_packets" : "";
        var args = $"-y -hide_banner -loglevel panic -strict experimental -vn -protocol_whitelist pipe,file,http,https,tcp,tls,crypto {inputFormatOption} {formatSpecificInputOptions} -i {input} -f s16le {formatSpecificOutputOptions} -ac 2 -ar 48000 pipe:1";

        proc = new()
        {
            StartInfo = new ProcessStartInfo()
            {
                FileName = "ffmpeg",
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardError = false,
                RedirectStandardInput = isInputPiped,
                RedirectStandardOutput = true,
                CreateNoWindow = true,
            }
        };

        proc.Start();

        var streams = new ProcessorStreams { Input = isInputPiped ? proc.StandardInput.BaseStream : null, Output = proc.StandardOutput.BaseStream };
        return Task.FromResult(streams);
    }
}
