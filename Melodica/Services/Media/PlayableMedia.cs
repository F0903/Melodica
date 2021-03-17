using System;
using System.IO;
using System.Threading.Tasks;

using Melodica.Services.Serialization;
using Melodica.Utility.Extensions;

using Microsoft.EntityFrameworkCore.Metadata;

namespace Melodica.Services.Media
{
    public class PlayableMedia
    {
        public delegate Task<(Stream data, string format)> DataGetter(PlayableMedia self);

        public delegate Task<PlayableMedia> DataRequester(PlayableMedia self);

        public PlayableMedia(MediaInfo info, MediaInfo? collectionInfo, DataGetter? dataGetter)
        {
            Info = info;
            CollectionInfo = collectionInfo;
            this.dataGetter = dataGetter;
        }

        public MediaInfo Info { get; set; }

        [NonSerialized]
        MediaInfo? collectionInfo;
        public MediaInfo? CollectionInfo { get => collectionInfo; set => collectionInfo = value; }

        public event DataRequester? OnDataRequested;

        private readonly DataGetter? dataGetter;

        public static ValueTask<PlayableMedia> FromExistingInfo(MediaInfo info)
        {
            return ValueTask.FromResult(new PlayableMedia(info));
        }

        private PlayableMedia(MediaInfo meta) => Info = meta;

        /// <summary>
        /// Downloads the media data on demand. Call this before accessing DataInformation.
        /// </summary>
        /// <returns></returns>
        public async Task DownloadDataAsync()
        {
            if (OnDataRequested is null)
                return;
            var cachedMedia = await OnDataRequested(this);
            Info = cachedMedia.Info;
            return;
        }

        /// <summary>
        /// Saves data to disk. Should only be called by MediaCache.
        /// </summary>
        /// <param name="saveDir"></param>
        /// <returns></returns>
        public virtual async Task<(string mediaPath, string metaPath)> SaveDataAsync(string saveDir)
        {
            if (dataGetter is null)
                return ("", "");

            if (saveDir is null)
                throw new NullReferenceException("SaveDir was not set.");

            var id = Info.Id;
            if (id is null)
                throw new NullReferenceException("Tried to save media with empty ID.");

            var legalId = id.ReplaceIllegalCharacters();

            // Write the media data to file.
            var (data, format) = await dataGetter(this);
            var fileExt = $".{format}";
            string? mediaLocation = Path.Combine(saveDir, legalId + fileExt);
            using var file = File.OpenWrite(mediaLocation);
            using (data) await data.CopyToAsync(file);
            await file.FlushAsync();
            var dataInfo = Info.DataInformation;
            Info.DataInformation = new(format) { MediaPath = mediaLocation };

            // Serialize the metadata.
            string? metaLocation = Path.Combine(saveDir!, legalId + MediaInfo.MetaFileExtension);
            var bs = new BinarySerializer();
            await bs.SerializeToFileAsync(metaLocation, Info);
            return (mediaLocation, metaLocation);
        }
    }
}