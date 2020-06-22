using Suits.Jukebox.Services.Cache;
using Suits.Jukebox.Services.Downloaders;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Suits.Utility.Extensions;
using Suits.Jukebox.Models.Exceptions;

namespace Suits.Jukebox.Models.Requests
{
    public class DownloadRequest : MediaRequest
    {
        public DownloadRequest(string query, IAsyncDownloader downloader)
        {
            this.query = query;
            this.downloader = downloader;
            
            // Make this check more efficient, possibly by using regex instead of querying
            MediaType = this.downloader.EvaluateMediaTypeAsync(query).Result;
            if (MediaType == MediaType.Playlist)
            {
                var (pl, videos) = this.downloader.DownloadPlaylistInfoAsync(query).Result;
                cachedInfo = pl;
                for (int i = 0; i < videos.Count(); i++)
                {
                    var item = videos.ElementAt(i);
                    if (item.MediaOrigin == null) throw new CriticalException("MediaOrigin is not specified.");
                    SubRequests.Add(new DownloadRequest(item, item.MediaOrigin!.SupportsDirectDownload ? this.downloader : IAsyncDownloader.Default));
                }
            }
        }

        private DownloadRequest(MediaMetadata info, IAsyncDownloader dl)
        {
            downloader = dl;
            this.cachedInfo = info;
            query = info.ID!;
        }

        public const int MaxExceptionRetries = 3;
        private int currentExceptionRetries = 0;

        private readonly IAsyncDownloader downloader;

        private readonly string query;

        private MediaMetadata? cachedInfo;
        public override MediaMetadata GetInfo() => cachedInfo ??= downloader.GetMediaInfoAsync(query).Result;

        public async override Task<PlayableMedia> GetMediaAsync()
        {
            Task<PlayableMedia> GetVideoAsync(string? id = null)
            {
                PlayableMedia media;
                try
                {
                    media = GetInfo().MediaType == MediaType.Livestream     ?
                            downloader.DownloadAsync(query).Result          :
                            MediaCache.Contains(id ?? GetInfo().ID!)        ?
                            MediaCache.GetAsync(id ?? GetInfo().ID!).Result :                       
                            MediaCache.CacheMediaAsync(downloader.DownloadAsync(query).Result).Result;
                }
                catch (Exception ex)
                {
                    if (ex is MediaUnavailableException || ex.InnerException is MediaUnavailableException)
                        throw;
                    if (currentExceptionRetries++ > MaxExceptionRetries)
                        throw new Exception("Max exception retry attempts reached. The specified media could not be retrieved.", ex);
                    return GetVideoAsync(id);
                }
                return Task.FromResult(media);
            }

            return GetInfo().MediaType switch
            {
                MediaType.Video => await GetVideoAsync(),
                MediaType.Playlist => await GetVideoAsync(SubRequests.First().GetInfo().ID),
                MediaType.Livestream => await GetVideoAsync(),
                _ => throw new Exception("Unknown error occured in GetMediaAsync(). (MediaType has probably not been set)"),
            };
        }
    }
}
