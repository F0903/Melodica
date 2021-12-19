using System.Diagnostics;

using Melodica.Core.Exceptions;
using Melodica.Services.Caching;
using Melodica.Services.Media;

namespace Melodica.Services.Audio;

public class FFmpegAudioProcessor : AudioProcessor
{
    public FFmpegAudioProcessor() : base(input, output)
    { }

    private const bool input = false;
    private const bool output = true;

    protected override Task<Process> CreateAsync(DataInfo dataInfo, TimeSpan? startingPoint = null)
    {
        if (dataInfo is null)
            throw new NullReferenceException("DataInfo of media was null.");

        if (string.IsNullOrEmpty(dataInfo.MediaPath))
        {
            MediaFileCache.ClearAllCachesAsync().Wait();
            throw new CriticalException("Song path is empty... Clearing cache... (something went wrong here)");
        }

        string? format = dataInfo.Format != null ? $"-f {dataInfo.Format}" : string.Empty;
        string? path = dataInfo.MediaPath != null ? $"\"{dataInfo.MediaPath}\"" : "pipe:0";
        startingPoint ??= TimeSpan.Zero;

        Process? proc = new Process()
        {
            StartInfo = new ProcessStartInfo()
            {
                FileName = "ffmpeg.exe",
                Arguments = $"-y -hide_banner -loglevel error -fflags nobuffer -fflags discardcorrupt -flags low_delay -avioflags direct -strict experimental -vn {format} -ss {startingPoint} -i {path} -f s16le -ac 2 -ar 48000 pipe:1",
                UseShellExecute = false,
                RedirectStandardError = false,
                RedirectStandardInput = input,
                RedirectStandardOutput = output,
                CreateNoWindow = true,
            }
        };
        return Task.FromResult(proc);
    }
}
