using System;
using System.Diagnostics;
using System.Threading.Tasks;

using Melodica.Core.Exceptions;
using Melodica.Services.Caching;

namespace Melodica.Services.Audio
{
    public class FFmpegAudioProcessor : AudioProcessor
    {
        public FFmpegAudioProcessor() : base(input, output)
        {
        }

        private const bool input = false;
        private const bool output = true;

        protected override Task<Process> ConstructAsync(string path, string? format, TimeSpan? startingPoint = null)
        {
            if (path == null || path == string.Empty)
            {
                MediaFileCache.ClearAllCachesAsync().Wait();
                throw new CriticalException("Song path is empty... Clearing cache... (something went wrong here)");
            }
            var proc = new Process()
            {
                StartInfo = new ProcessStartInfo()
                {
                    FileName = "ffmpeg.exe",
                    Arguments = $"-y -hide_banner -loglevel debug -fflags nobuffer -fflags discardcorrupt -flags low_delay -strict experimental -avioflags direct -vn {(format != null ? $"-f {format}" : string.Empty)} -ss {startingPoint ?? TimeSpan.Zero} -i {(path != null ? $"\"{path}\"" : "pipe:0")} -f s16le -ac 2 -ar 48000 pipe:1",
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
}