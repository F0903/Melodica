using Suits.Jukebox.Services.Cache;
using Suits.Jukebox.Services.Downloaders;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Suits.Jukebox.Models.Requests
{
    public class DownloadMediaRequest : MediaRequest
    {
        public DownloadMediaRequest(string query, MediaCache cache, Discord.IGuild guild, QueueMode mode = QueueMode.Consistent,
                                    Action? largeSizeWarning = null, Action<string>? videoUnavailable = null,
                                    IAsyncDownloadService? downloader = null)
        {
            this.downloader = downloader ?? IoC.Kernel.Get<IAsyncDownloadService>();
            this.query = query;
            this.guild = guild;
            this.mode = mode;
            this.cache = cache;
            this.largeSizeWarning = largeSizeWarning;
            this.videoUnavailable = videoUnavailable;
        }

        private readonly string query;

        private readonly Discord.IGuild guild;

        private readonly IAsyncDownloadService downloader;

        private readonly QueueMode mode;

        private readonly MediaCache cache;

        readonly Action? largeSizeWarning;

        readonly Action<string>? videoUnavailable;

        public override async Task<MediaCollection> GetMediaRequestAsync()
        {
            return await downloader.DownloadToCacheAsync(cache, mode, guild, query, true, largeSizeWarning!, videoUnavailable!);
        }
    }
}
