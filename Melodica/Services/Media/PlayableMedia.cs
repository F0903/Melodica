using System;
using System.IO;
using System.Threading.Tasks;

using Melodica.Services.Serialization;
using Melodica.Utility.Extensions;

namespace Melodica.Services.Media
{
    public class PlayableMedia
    {
        public delegate Task<(Stream data, string format)> DataGetter(PlayableMedia self);

        public delegate Task<PlayableMedia> DataInfoRequester(PlayableMedia self);

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

        public event DataInfoRequester? OnDataInfoRequested;

        private readonly DataGetter? dataGetter;

        public static ValueTask<PlayableMedia> FromExistingInfo(MediaInfo info)
        {
            if (info.DataInformation.MediaPath is null)
                throw new NullReferenceException("Cannot create PlayableMedia from existing info that has no mediapath.");
            return ValueTask.FromResult(new PlayableMedia(info));
        }

        private PlayableMedia(MediaInfo meta) => Info = meta;

        // Call before accessing datainfo, as this caches stuff on demand.
        public async Task RequestDataInfoAsync()
        {
            if (OnDataInfoRequested is null)
                return;
            var cachedMedia = await OnDataInfoRequested(this);
            Info = cachedMedia.Info;
            return;
        }

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
            using (data)
            {
                await data.CopyToAsync(file);
                Info.DataInformation.Format = format;
            }
            await file.FlushAsync();

            Info.DataInformation.MediaPath = mediaLocation;

            // Serialize the metadata.
            string? metaLocation = Path.Combine(saveDir!, legalId + MediaInfo.MetaFileExtension);
            var bs = new BinarySerializer();
            await bs.SerializeToFileAsync(metaLocation, Info);
            return (mediaLocation, metaLocation);
        }
    }
}