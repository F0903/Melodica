using Melodica.Services.Services;
using Melodica.Services.Services.Downloaders;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Melodica.Utility.Extensions;
using Melodica.Services.Downloaders.Exceptions;
using System.Threading;
using System.ComponentModel;
using Ninject.Activation.Caching;
using Melodica.Services.Jukebox.Models;
using Microsoft.EntityFrameworkCore;
using Melodica.Services.Downloaders;

namespace Melodica.Services.Jukebox.Models.Requests
{
    public class DownloadRequest : MediaRequestBase
    {
        public DownloadRequest(string query, AsyncDownloaderBase dl)
        {
            downloader = dl;
            this.query = query;

            if (dl.IsPlaylistAsync(query))
            {
                SubRequests = new List<MediaRequestBase>();
                var (playlistInfo, videos) = this.downloader.DownloadPlaylistInfoAsync(this.query).Result;
                info = playlistInfo;
                for (int i = 0; i < videos.Count(); i++)
                {
                    var item = videos.ElementAt(i);
                    SubRequests!.Add(new DownloadRequest(item, info, item.MediaOrigin.HandlesDownloads ? downloader : AsyncDownloaderBase.Default));
                }
            }
        }

        private DownloadRequest(MediaMetadata info, MediaMetadata parentRequestInfo, AsyncDownloaderBase dl)
        {
            this.info = info;
            this.query = info.ID!;

            SubRequestInfo = new SubRequestInfo()
            {
                ParentRequestInfo = parentRequestInfo
            };

            downloader = dl;
            this.info = info;
        }

        public const int MaxExceptionRetries = 3;
        private int currentExceptionRetries = 0;

        private readonly AsyncDownloaderBase downloader;

        private readonly string query;

        public override SubRequestInfo? SubRequestInfo { get; protected set; }

        public override List<MediaRequestBase>? SubRequests { get; set; }

        private MediaMetadata? info;
        public override MediaMetadata GetInfo() => info ??= downloader.GetMediaInfoAsync(query).Result;

        public async override Task<PlayableMedia> GetMediaAsync()
        {
            Task<PlayableMedia> GetVideoAsync(string? id = null)
            {
                PlayableMedia media;
                try
                {
                    media = downloader.DownloadAsync(query).Result;
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
