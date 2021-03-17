using System;
using System.Diagnostics;
using System.Threading.Tasks;

using Melodica.Core.Exceptions;
using Melodica.Services.Caching;
using Melodica.Services.Media;

namespace Melodica.Services.Audio
{
    public class FFmpegAudioProcessor : AudioProcessor
    {
        public FFmpegAudioProcessor() : base(input, output)
        {}

        private const bool input = false;
        private const bool output = true;

        protected override async Task<Process> CreateAsync(PlayableMedia media, TimeSpan? startingPoint = null)
        {
            await media.DownloadDataAsync();
            var dataInfo = media.Info.DataInformation;
            if (dataInfo is null)
                throw new NullReferenceException("DataInfo of media was null.");

            if (string.IsNullOrEmpty(dataInfo.MediaPath))
            {
                MediaFileCache.ClearAllCachesAsync().Wait();
                throw new CriticalException("Song path is empty... Clearing cache... (something went wrong here)");
            }

            var format = dataInfo.Format != null ? $"-f {dataInfo.Format}" : string.Empty;
            var path = dataInfo.MediaPath != null ? $"\"{dataInfo.MediaPath}\"" : "pipe:0";
            startingPoint ??= TimeSpan.Zero;

            var proc = new Process()
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
            return proc;
        }
    }
}