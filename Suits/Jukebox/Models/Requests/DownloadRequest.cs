using Suits.Jukebox.Services.Cache;
using Suits.Jukebox.Services.Downloaders;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Suits.Utility.Extensions;
using Suits.Jukebox.Models.Exceptions;
using System.Threading;
using System.ComponentModel;
using Ninject.Activation.Caching;

namespace Suits.Jukebox.Models.Requests
{
    public class DownloadRequest : MediaRequest
    {
        public DownloadRequest(string query, IAsyncDownloader dl)
        {
            downloader = dl;

            info = downloader.GetMediaInfoAsync(query).Result;
            RequestMediaType = info.MediaType;

            SubRequestInfo = new SubRequestInfo()
            {
                IsSubRequest = false,
                ParentRequestInfo = null
            };

            if (RequestMediaType == MediaType.Playlist)
            {
                SubRequests = new List<MediaRequest>();
                var (pl, videos) = this.downloader.DownloadPlaylistInfoAsync(query).Result;
                info = pl;
                for (int i = 0; i < videos.Count(); i++)
                {
                    var item = videos.ElementAt(i);
                    if (item.MediaOrigin == null) throw new CriticalException("MediaOrigin is not specified.");
                    SubRequests!.Add(new DownloadRequest(item, info, item.MediaOrigin!.SupportsDirectDownload ? this.downloader : IAsyncDownloader.Default));
                }
            }
        }

        private DownloadRequest(MediaMetadata info, MediaMetadata parentRequestInfo, IAsyncDownloader dl)
        {
            this.info = info;
            RequestMediaType = info.MediaType;

            SubRequestInfo = new SubRequestInfo()
            {
                IsSubRequest = true,
                ParentRequestInfo = parentRequestInfo
            };

            downloader = dl;
            this.info = info;
        }

        public const int MaxExceptionRetries = 3;
        private int currentExceptionRetries = 0;

        private readonly IAsyncDownloader downloader;

        private readonly MediaMetadata info;

        public override MediaType RequestMediaType { get; protected set; }
        public override SubRequestInfo? SubRequestInfo { get; protected set; }
        public override List<MediaRequest>? SubRequests { get; set; }

        public override MediaMetadata GetInfo() => info;

        public async override Task<PlayableMedia> GetMediaAsync()
        {
            Task<PlayableMedia> GetVideoAsync(string? id = null)
            {
                PlayableMedia media;
                try
                {
                    media = downloader.DownloadAsync(info).Result;
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
                MediaType.Playlist => throw new CriticalException("Cannot directly play a playlist. Something very wrong happened here..."),
                MediaType.Livestream => await GetVideoAsync(),
                _ => throw new Exception("Unknown error occured in GetMediaAsync(). (MediaType has probably not been set)"),
            };
        }
    }
}
