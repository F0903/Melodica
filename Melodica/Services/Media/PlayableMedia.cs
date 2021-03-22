using System;
using System.IO;
using System.Threading.Tasks;

using Melodica.Services.Serialization;
using Melodica.Utility.Extensions;

namespace Melodica.Services.Media
{
    public record DataPair(Stream? Data, DataInfo Info);

    public delegate Task<DataPair> DataGetter(PlayableMedia media);

    public record DataInfo(string Format, string MediaPath)
    {
        public string FileExtension => Format.Insert(0, ".");
    }

    public class PlayableMedia
    {
        private PlayableMedia(MediaInfo meta) => Info = meta;

        public PlayableMedia(MediaInfo info, MediaInfo? collectionInfo, DataGetter dataGetter)
        {
            Info = info;
            CollectionInfo = collectionInfo;
            this.dataGetter = dataGetter;
        }

        [NonSerialized]
        private readonly DataGetter? dataGetter;

        [NonSerialized]
        MediaInfo? collectionInfo;
        public MediaInfo? CollectionInfo { get => collectionInfo; set => collectionInfo = value; }

        public MediaInfo Info { get; set; }

        DataPair? cachedData;
        public DataInfo DataInfo
        {
            get
            {
                var pair = cachedData ?? LoadData().GetAwaiter().GetResult();
                return pair.Info;
            }
        }

        public static ValueTask<PlayableMedia> FromExistingInfo(MediaInfo info)
        {
            return ValueTask.FromResult(new PlayableMedia(info));
        }

        async Task<DataPair> LoadData()
        {
            //TODO: Make this stuff not recurse forever.
            if (dataGetter is null)
                throw new NullReferenceException("DataGetter was null. (1)");

            return cachedData ??= await dataGetter(this);
        }

        /// <summary>
        /// Saves data to disk. Should only be called through MediaCache.
        /// </summary>
        /// <param name="saveDir"></param>
        /// <returns></returns>
        public virtual async Task<string> SaveDataAsync(string saveDir)
        {
            if (saveDir is null)
                throw new NullReferenceException("SaveDir was not set.");

            var id = Info.Id;
            if (id is null)
                throw new NullReferenceException("Tried to save media with empty ID.");

            var legalId = id.ReplaceIllegalCharacters();

            // Write the media data to file.
            var (s, info) = await LoadData();
            if (s is null)
                throw new NullReferenceException("Data stream was null.");
            using var stream = s;
            var format = info.Format;
            var fileExt = $".{format}";

            var mediaLocation = Path.Combine(saveDir, legalId + fileExt);

            using var file = File.OpenWrite(mediaLocation);
            await stream.CopyToAsync(file);
            await file.FlushAsync();

            // Serialize the metadata.
            string metaLocation = Path.Combine(saveDir!, legalId + MediaInfo.MetaFileExtension);
            var bs = new BinarySerializer();
            await bs.SerializeToFileAsync(metaLocation, Info);

            return mediaLocation;
        }
    }
}