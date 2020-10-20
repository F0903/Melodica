using System;
using System.Diagnostics;

using Melodica.Core.Exceptions;
using Melodica.Services.Services;

namespace Melodica.Services.Audio
{
    public class FFmpegAudioProcessor : ExternalAudioProcessor
    {
        public FFmpegAudioProcessor(string mediaPath, string? format = null, TimeSpan? startingPoint = null) : base(mediaPath, format, startingPoint)
        { }

        protected override Process ConstructExternal(string path, string? format = null, TimeSpan? startingPoint = null)
        {
            if (path == null || path == string.Empty)
            {
                MediaFileCache.ClearAllCachesAsync().Wait();
                throw new CriticalException("Song path is empty... Clearing cache... (something went wrong here)");
            }
            return new Process()
            {
                StartInfo = new ProcessStartInfo()
                {
                    FileName = "ffmpeg.exe",
                    Arguments = $"-y -hide_banner -loglevel debug -fflags nobuffer -fflags discardcorrupt -flags low_delay -strict experimental -avioflags direct -vn {(format != null ? $"-f {format}" : string.Empty)} -ss {startingPoint ?? TimeSpan.Zero} -i {(path != null ? $"\"{path}\"" : "pipe:0")} -f s16le -ac 2 -ar 48000 pipe:1",
                    UseShellExecute = false,
                    RedirectStandardError = false,
                    RedirectStandardInput = (inputAvailable = (path == null)),
                    RedirectStandardOutput = (outputAvailable = true),
                    CreateNoWindow = true,
                }
            };
        }
    }
}
