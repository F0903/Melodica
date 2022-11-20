using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Melodica.Core.Exceptions;
using Melodica.Services.Caching;

using Melodica.Services.Media;

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
        var formatString = inputFormat is not null ? $"-f {inputFormat}" : "";

        proc = new()
        {
            StartInfo = new ProcessStartInfo()
            {
                FileName = "ffmpeg",
                Arguments = $"-y -hide_banner -loglevel error -fflags discardcorrupt -flags low_delay -strict experimental -vn {formatString} -protocol_whitelist pipe,file,http,https,tcp,tls,crypto -i {input} -f s16le -ac 2 -ar 48000 pipe:1",
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
