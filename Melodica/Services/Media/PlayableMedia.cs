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
            this.data = data;
        }

        public MediaInfo Info { get; set; }

        private readonly Stream? data;

        public static async Task<PlayableMedia> LoadFromFileAsync(string songPath)
        {
            songPath = Path.ChangeExtension(songPath, MediaInfo.MetaFileExtension);
            if (!File.Exists(songPath))
                throw new FileNotFoundException($"Metadata file was not found.");

            var meta = await MediaInfo.LoadFromFile(songPath);
            if (!File.Exists(meta.DataInformation.MediaPath))
                throw new FileNotFoundException("The associated media file of this metadata does not exist.");

            return new PlayableMedia(meta);
        }

        private PlayableMedia(MediaInfo meta) => Info = meta;

        public virtual async Task<(string mediaPath, string metaPath)> SaveDataAsync(string saveDir)
        {
            if (data is null)
                return ("", "");

            if (saveDir is null)
                throw new NullReferenceException("SaveDir was not set.");

            var id = Info.Id;
            if (id is null)
                throw new NullReferenceException("Tried to save media with empty ID.");

            var legalId = id.ReplaceIllegalCharacters();

            var fileExt = Info.DataInformation.FileExtension;
            if (fileExt is null)
                throw new NullReferenceException("File extension was null.");

            // Write the media data to file.
            string? mediaLocation = Path.Combine(saveDir, legalId + fileExt);
            using var file = File.OpenWrite(mediaLocation);
            using (data)
            {
                await data.CopyToAsync(file);
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