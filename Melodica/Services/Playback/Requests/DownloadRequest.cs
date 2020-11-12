using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Melodica.Services.Downloaders;
using Melodica.Services.Downloaders.Exceptions;
using Melodica.Services.Downloaders.YouTube;
using Melodica.Services.Models;

namespace Melodica.Services.Playback.Requests
{
    public class DownloadRequest : MediaRequest
    {
        public DownloadRequest(string query) : this(query, new AsyncYoutubeDownloader()) { }

        public DownloadRequest(string query, AsyncDownloaderBase dl)
        {
            downloader = dl;
            this.query = query;

            if (dl.IsUrlPlaylistAsync(query))
            {
                SubRequests = new List<MediaRequest>();
                var (playlistInfo, videos) = downloader.DownloadPlaylistInfoAsync(this.query).Result;
                info = playlistInfo;
                for (int i = 0; i < videos.Count(); i++)
                {
                    var item = videos.ElementAt(i);
                    SubRequests!.Add(new DownloadRequest(item, info, item.Origin.SupportsDirectDownloads ? downloader : AsyncDownloaderBase.Default));
                }
            }
        }

        private DownloadRequest(MediaMetadata info, MediaMetadata parentRequestInfo, AsyncDownloaderBase dl)
        {
            this.info = info;
            query = info.Url!;

            ParentRequestInfo = parentRequestInfo;

            downloader = dl;
            this.info = info;
        }

        public const int MaxExceptionRetries = 3;
        private int currentExceptionRetries = 0;

        private readonly AsyncDownloaderBase downloader;

        private readonly string query;

        public override MediaMetadata? ParentRequestInfo { get; protected set; }

        public override List<MediaRequest>? SubRequests { get; set; }

        private MediaMetadata? info;
        public override MediaMetadata GetInfo() => info ??= downloader.GetMediaInfoAsync(query).Result;

        public override async Task<PlayableMedia> GetMediaAsync()
        {
            Task<PlayableMedia> GetVideoAsync(string? id = null)
            {
                PlayableMedia media;
                try
                {
                    if (info != null && info!.MediaType == MediaType.Livestream) // Quicker than the alternative as livestreams don't need downloads, and skips uneccesary calls to APIs
                        return Task.FromResult(new PlayableMedia(info, null));

                    media = info != null ? downloader.DownloadAsync(info).Result : downloader.DownloadAsync(query).Result;
                    info = media.Info;
                }
                catch (Exception ex)
                {
                    if (ex is MediaUnavailableException || ex.InnerException is MediaUnavailableException)
                        throw;

                    if (currentExceptionRetries++ > MaxExceptionRetries)
                        throw new Exception("Max exception retry attempts reached. The specified media could not be retrieved.\nIf this issue persists, please contact the bot owner.", ex);

                    return GetVideoAsync(id); // Retry once more.
                }
                return Task.FromResult(media);
            }
            return await GetVideoAsync();
        }
    }
}
