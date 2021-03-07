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

        public PlayableMedia(MediaInfo meta, DataGetter? dataGetter)
        {
            Info = meta;
            this.dataGetter = dataGetter;
        }

        public MediaInfo Info { get; set; }
        
        public event Func<PlayableMedia, Task>? OnDataRequested;

        private readonly DataGetter? dataGetter;

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

        public Task RequestDataAsync()
        {
            if (OnDataRequested is null)
                return Task.CompletedTask;
            return OnDataRequested(this);
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