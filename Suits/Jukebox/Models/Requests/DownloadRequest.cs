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
                this.playlistQuery = query;
                var pl = downloader.GetPlaylistVideoURLsAsync(query).Result;
                for (int i = 0; i < pl.Count(); i++)
                {
                    var item = pl.ElementAt(i);
                    if (i == 0)
                    {
                        this.query = item;
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

        private readonly Downloader downloader;

        private readonly string? playlistQuery = null;

        private readonly string query;

        public override IMediaInfo GetMediaInfo() => downloader.GetMediaInfoAsync(playlistQuery ?? query).Result;

        public async override Task<PlayableMedia> GetMediaAsync()
        {
            var media = await downloader.DownloadAsync(query);
            return (await MediaCache.CacheMediaAsync(media)).First();
        }
    }
}
