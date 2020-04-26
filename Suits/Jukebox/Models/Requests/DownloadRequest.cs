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
    public class DownloadRequest<Downloader> : MediaRequest where Downloader : class, IAsyncDownloader, new()
    {
        public DownloadRequest(string query, Downloader? dl = null)
        {
            this.query = query;
            downloader = dl ?? new Downloader();

            Type = downloader.EvaluateMediaTypeAsync(query).Result;
            if (Type == MediaType.Playlist)
            {
                Requests.Remove(this);
                var (pl, videos) = downloader.DownloadPlaylistInfoAsync(query).Result;
                info = pl;
                for (int i = 0; i < videos.Count(); i++)
                {
                    var item = videos.ElementAt(i);
                    Requests.Add(new DownloadRequest<Downloader>(item, downloader));
                }
            }
        }

        private DownloadRequest(Metadata info, Downloader dl)
        {
            Type = MediaType.Video;
            downloader = dl;
            this.info = info;
            query = info.ID!;
        }

        private readonly Downloader downloader;

        private readonly string query;

        private Metadata? info;
        public override Metadata GetMediaInfo() => info ??= downloader.GetMediaInfoAsync(query).Result;

        public async override Task<PlayableMedia> GetMediaAsync()
        {
            async Task<PlayableMedia> GetVideoAsync(string? id = null) => MediaCache.Contains(id ?? GetMediaInfo().ID!) ? await MediaCache.GetAsync(id ?? GetMediaInfo().ID!) : await MediaCache.CacheMediaAsync((await downloader.DownloadAsync(query)).First());

            switch (Type)
            {
                case MediaType.Video:
                    return await GetVideoAsync();               
                case MediaType.Playlist:
                    return await GetVideoAsync(Requests.First().GetMediaInfo().ID);
                case MediaType.Livestream:
                    var hlsUrl = await downloader.GetLivestreamAsync(query);
                    var info = GetMediaInfo();
                    info.MediaPath = hlsUrl;
                    info.Format = "hls";
                    return new PlayableMedia(info, null);
                default:
                    throw new Exception("Unknown error occured in GetMediaAsync(). (Type has probably not been set)");
            }
        }
    }
}
