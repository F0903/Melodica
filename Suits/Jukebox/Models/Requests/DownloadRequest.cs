using Suits.Jukebox.Services.Cache;
using Suits.Jukebox.Services.Downloaders;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Suits.Utility.Extensions;

namespace Suits.Jukebox.Models.Requests
{
    public class DownloadRequest<Downloader> : MediaRequest where Downloader : IAsyncDownloader, new()
    {
        public DownloadRequest(string query)
        {
            this.query = query;
            downloader = new Downloader();

            if (query.IsUrl())
                if (!downloader.VerifyURLAsync(query).Result)
                    throw new Exception("URL is not valid.");

            if (IsPlaylist = downloader.IsPlaylistAsync(query).Result)
            {
                var (pl, videos) = downloader.DownloadPlaylistInfoAsync(query).Result;
                for (int i = 0; i < videos.Count(); i++)
                {
                    var item = videos.ElementAt(i);
                    if (i == 0)
                    {
                        this.info = pl; // Set first requests info to the playlists info.
                        this.query = item.GetID()!;
                        Requests.Add(this);
                        continue;
                    }
                    Requests.Add(new DownloadRequest<Downloader>(item));
                }
            }
            else
            {
                Requests.Add(this);
            }
        }

        private DownloadRequest(IMediaInfo info)
        {
            downloader = new Downloader();
            this.info = info;
            query = info.GetID()!;
        }

        private readonly Downloader downloader;

        private readonly string query;

        private IMediaInfo? info;
        public override IMediaInfo GetMediaInfo() => info ?? (info = downloader.DownloadMediaInfoAsync(query).Result);

        public async override Task<PlayableMedia> GetMediaAsync()
        {
            var media = await downloader.DownloadAsync(query);
            info ??= media;
            return (await MediaCache.CacheMediaAsync(media)).First();
        }
    }
}
