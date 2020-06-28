using System;
using System.IO;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using Suits.Utility.Extensions;

namespace Suits.Jukebox.Models
{
    public class PlayableMedia
    {
        public PlayableMedia(MediaMetadata meta, Stream? data)
        {
            this.Info = meta;
            this.rawMediaData = data;
        }

        public PlayableMedia(PlayableMedia toCopy)
        {
            Info = toCopy.Info;
            rawMediaData = toCopy.rawMediaData;
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

        protected string? saveDir;

        protected virtual async Task SaveDataAsync()
        {
            if (rawMediaData == null)
                return;

            var location = Path.Combine(saveDir ?? throw new NullReferenceException("SaveDir was not set."), (Info.ID ?? throw new Exception("Tried to save media with empty ID.")).ReplaceIllegalCharacters() + Info.DataInformation.FileExtension);
            if (rawMediaData == null) throw new Exception("Media data was null.");

            using var fs = File.OpenWrite(location);
            await rawMediaData.CopyToAsync(fs);

            await fs.FlushAsync();
            rawMediaData = null;

            Info.DataInformation.MediaPath = location;
        }

        public string? GetThumbnail() => Info.Thumbnail;
        public string? GetID() => Info.ID;
        public string? GetTitle() => Info.Title;
        public string? GetURL() => Info.URL;
        public TimeSpan GetDuration() => Info.Duration;
        public string? GetFormat() => Info.DataInformation.Format;
    }
}