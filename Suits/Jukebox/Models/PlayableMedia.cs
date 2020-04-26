using System;
using System.IO;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using Suits.Utility.Extensions;

namespace Suits.Jukebox.Models
{
    public class PlayableMedia
    {
        public PlayableMedia(Metadata meta, byte[]? data)
        {
            this.Info = meta;
            this.mediaData = data;
            saveable = data != null;
        }

        public PlayableMedia(PlayableMedia toCopy)
        {
            Info = toCopy.Info;
            mediaData = toCopy.mediaData;
        }

        private PlayableMedia(Metadata meta)
        {
            Info = meta;
        }

        public static Task<PlayableMedia> LoadFromFileAsync(string songPath)
        {
            songPath = Path.ChangeExtension(songPath, Metadata.MetaFileExtension);
            if (!File.Exists(songPath))
                throw new FileNotFoundException($"Metadata file was not found.");

            var meta = Metadata.LoadFromFile(songPath);
            if (!File.Exists(meta.MediaPath))
                throw new FileNotFoundException("The associated media file of this metadata does not exist.");

            return Task.FromResult(new PlayableMedia(meta));
        }

        public virtual Metadata Info { get; protected set; }

        private byte[]? mediaData;

        protected string? saveDir;

        private readonly bool saveable = true;

        protected virtual Task SaveDataAsync()
        {
            if (!saveable)
                return Task.CompletedTask;
            var location = Path.Combine(saveDir ?? throw new NullReferenceException("Please set saveDir before saving."), (Info.ID ?? throw new Exception("Tried to save media with empty ID.")).ReplaceIllegalCharacters() + Info.FileExtension);
            if (mediaData == null) throw new Exception("Media data was null.");
            File.WriteAllBytes(location, mediaData);
            mediaData = null;
            Info.MediaPath = location;
            return Task.CompletedTask;
        }

        public string? GetThumbnail() => Info.Thumbnail;
        public string? GetID() => Info.ID;
        public string? GetTitle() => Info.Title;
        public string? GetURL() => Info.URL;
        public TimeSpan GetDuration() => Info.Duration;
        public string? GetFormat() => Info.Format;
    }
}