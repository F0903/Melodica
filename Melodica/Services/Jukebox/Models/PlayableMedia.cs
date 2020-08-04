using System;
using System.IO;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using Melodica.Services;
using Melodica.Services.Serialization;
using Melodica.Utility.Extensions;

namespace Melodica.Services.Jukebox.Models
{
    public class PlayableMedia
    {
        public PlayableMedia(MediaMetadata meta, Stream? data)
        {
            this.Info = meta;
            this.rawMediaData = data;
        }

        public PlayableMedia(PlayableMedia other, MediaMetadata newMeta)
        {
            this.rawMediaData = other.rawMediaData;
            this.Info = newMeta;
        }

        private PlayableMedia(MediaMetadata meta)
        {
            Info = meta;
        }

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
            var mediaLocation = Path.Combine(saveDir ?? throw new NullReferenceException("SaveDir was not set."), (Info.ID ?? throw new Exception("Tried to save media with empty ID.")).ReplaceIllegalCharacters() + Info.DataInformation.FileExtension);  
            using var file = File.OpenWrite(mediaLocation);
            await rawMediaData.CopyToAsync(file);
            await file.FlushAsync();
            rawMediaData = null;
            Info.DataInformation.MediaPath = mediaLocation;

            // Serialize the metadata.
            var metaLocation = Path.Combine(saveDir!, (Info.ID ?? throw new NullReferenceException("Tried to save media with empty ID.")).ReplaceIllegalCharacters() + MediaMetadata.MetaFileExtension);
            var bs = new BinarySerializer();
            await bs.SerializeToFileAsync(metaLocation, Info);            
        }
    }
}