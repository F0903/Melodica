﻿using System;
using System.IO;
using System.Threading.Tasks;

using Melodica.Services.Caching;
using Melodica.Services.Serialization;
using Melodica.Utility.Extensions;

namespace Melodica.Services.Media
{
    public record DataPair(Stream? Data, string Format);

    public delegate Task<DataPair> DataGetter(PlayableMedia media);

    public class PlayableMedia
    {
        private PlayableMedia(MediaInfo meta) => Info = meta;

        public PlayableMedia(MediaInfo info, MediaInfo? collectionInfo, DataGetter dataGetter, IMediaCache? cache)
        {
            Info = info;
            CollectionInfo = collectionInfo;
            this.dataGetter = dataGetter;
            this.cache = cache;
        }

        [NonSerialized]
        private readonly DataGetter? dataGetter;

        [NonSerialized]
        private readonly IMediaCache? cache;

        [NonSerialized]
        MediaInfo? collectionInfo;
        public MediaInfo? CollectionInfo { get => collectionInfo; set => collectionInfo = value; }

        public MediaInfo Info { get; set; }

        public static ValueTask<PlayableMedia> FromExisting(MediaInfo info)
        {
            var media = new PlayableMedia(info);
            return ValueTask.FromResult(media);
        }

        /// <summary>
        /// Saves data and returns info about it.
        /// </summary>
        /// <param name="saveDir"></param>
        /// <returns></returns>
        public virtual async Task<DataInfo> SaveDataAsync()
        {
            if (cache is null)
                throw new NullReferenceException("Cache was null.");

            if (cache.TryGet(Info.Id, out var cachedMedia))
            {
                var info = cachedMedia!.Info.DataInfo;
                if (info is not null)
                    return info;
            }

            if (dataGetter is null)
                throw new NullReferenceException("DataGetter was null. (1)");

            // Write the media data to file.
            if (Info.DataInfo is null)
            {
                var dataPair = await dataGetter(this);
                return Info.DataInfo = await cache.CacheAsync(this, dataPair);
            }
            return Info.DataInfo;
        }
    }
}