using System;
using System.IO;
using System.Threading.Tasks;

using Melodica.Services.Serialization;
using Melodica.Utility.Extensions;

namespace Melodica.Services.Models
{
    public class PlayableMedia
    {
        public PlayableMedia(MediaMetadata meta, Stream? data)
        {
            Info = meta;
            rawMediaData = data;
        }

        private PlayableMedia(MediaMetadata meta) => Info = meta;

        public static Task<PlayableMedia> LoadFromFileAsync(string songPath)
        {
            songPath = Path.ChangeExtension(songPath, MediaMetadata.MetaFileExtension);
            if (!File.Exists(songPath))
                throw new FileNotFoundException($"Metadata file was not found.");

            var meta = MediaMetadata.LoadFromFile(songPath);
            if (!File.Exists(meta.DataInformation.MediaPath))
                throw new FileNotFoundException("The associated media file of this metadata does not exist.");

            return Task.FromResult(new PlayableMedia(meta));
        }

        public virtual MediaMetadata Info { get; protected set; }

        private Stream? rawMediaData;

        public virtual async Task SaveDataAsync(string saveDir)
        {
            if (rawMediaData == null)
                return;

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
            string? metaLocation = Path.Combine(saveDir!, (Info.Id ?? throw new NullReferenceException("Tried to save media with empty ID.")).ReplaceIllegalCharacters() + MediaMetadata.MetaFileExtension);
            var bs = new BinarySerializer();
            await bs.SerializeToFileAsync(metaLocation, Info);
        }
    }
}