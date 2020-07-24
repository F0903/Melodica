﻿using Suits.Jukebox.Services;
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
using Suits.Jukebox.Models;

namespace Suits.Jukebox.Models.Requests
{
    public class DownloadRequest : MediaRequestBase
    {
        public DownloadRequest(string query, AsyncDownloaderBase dl)
        {
            downloader = dl;

            info = downloader.GetMediaInfoAsync(query).Result;
            RequestMediaType = info.MediaType;

            if (RequestMediaType == MediaType.Playlist)
            {
                SubRequests = new List<MediaRequestBase>();
                var (playlistInfo, videos) = this.downloader.DownloadPlaylistInfoAsync(query).Result;
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
            RequestMediaType = info.MediaType;

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

        private readonly MediaMetadata info;

        public override MediaType RequestMediaType { get; protected set; }

        public override SubRequestInfo? SubRequestInfo { get; protected set; }

        public override List<MediaRequestBase>? SubRequests { get; set; }

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
