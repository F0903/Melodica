using System;
using System.IO;
using System.Threading.Tasks;

using Melodica.Services.Serialization;
using Melodica.Utility.Extensions;

namespace Melodica.Services.Media
{
    public class PlayableMedia
    {
        public PlayableMedia(MediaInfo meta, Stream? data)
        {
            Info = meta;
            rawMediaData = data;
        }

        public virtual MediaInfo Info { get; protected set; }

        private Stream? rawMediaData;

        public static async Task<PlayableMedia> LoadFromFileAsync(string songPath)
        {
            songPath = Path.ChangeExtension(songPath, MediaInfo.MetaFileExtension);
            if (!File.Exists(songPath))
                throw new FileNotFoundException($"Metadata file was not found.");

            var meta = await MediaInfo.LoadFromFile(songPath);
            if (meta.DataInformation is null || !File.Exists(meta.DataInformation?.MediaPath))
                throw new FileNotFoundException("The associated media file of this metadata does not exist, or data info was null.");

            return new PlayableMedia(meta);
        }

        private PlayableMedia(MediaInfo meta) => Info = meta;

        public virtual async Task<(string mediaPath, string metaPath)> SaveDataAsync(string saveDir)
        {
            if (rawMediaData == null)
                return ("", "");

            // Write the media data to file.
            string? mediaLocation = Path.Combine(saveDir ?? throw new NullReferenceException("SaveDir was not set."), (Info.Id ?? throw new Exception("Tried to save media with empty ID.")).ReplaceIllegalCharacters() + Info.DataInformation.FileExtension);
            using var file = File.OpenWrite(mediaLocation);
            await rawMediaData.CopyToAsync(file);
            await file.FlushAsync();

            Info.DataInformation.MediaPath = mediaLocation;

            // Dispose of raw data, since it is now on the disk.
            rawMediaData.Dispose();
            rawMediaData = null;

            // Serialize the metadata.
            string? metaLocation = Path.Combine(saveDir!, (Info.Id ?? throw new NullReferenceException("Tried to save media with empty ID.")).ReplaceIllegalCharacters() + MediaInfo.MetaFileExtension);
            var bs = new BinarySerializer();
            await bs.SerializeToFileAsync(metaLocation, Info);
            return (mediaLocation, metaLocation);
        }
    }
}