using System.Diagnostics;

using Melodica.Core.Exceptions;
using Melodica.Services.Caching;
using Melodica.Services.Media;

using Serilog;

namespace Melodica.Services.Audio;

public sealed class FFmpegAudioProcessor : AudioProcessor
{
    public FFmpegAudioProcessor(bool pipeInput = false) : base(pipeInput, true)
    { } 

    //TODO: Implement startingpoint.
    protected override Task<Process> CreateAsync(DataInfo dataInfo, TimeSpan? startingPoint = null)
    {
        if (dataInfo is null)
            throw new NullReferenceException("DataInfo of media was null.");

        if (!inputPipe && string.IsNullOrEmpty(dataInfo.MediaPath))
        {
            MediaFileCache.ClearAllCachesAsync().Wait();
            throw new CriticalException("Song path is empty... Clearing cache... (something went wrong here)");
        }

        string format = dataInfo.Format ?? string.Empty;
        string path = !inputPipe ? $"\"{dataInfo.MediaPath}\"" : "pipe:0"; 

        //TODO: Fix strange slomo playback when playing already raw s16le

        Process proc = new()
        {
            StartInfo = new ProcessStartInfo()
            {
                FileName = "ffmpeg",
                Arguments = $"-y -hide_banner -loglevel error -fflags discardcorrupt -flags low_delay -strict experimental -vn -f {format} -protocol_whitelist pipe,file,http,https,tcp,tls,crypto -i {path} -f s16le -ac 2 -ar 48000 pipe:1",
                UseShellExecute = false,
                RedirectStandardError = false,
                RedirectStandardInput = inputPipe,
                RedirectStandardOutput = outputPipe,
                CreateNoWindow = true,
            }
        }; 

        return Task.FromResult(proc);
    }
}
