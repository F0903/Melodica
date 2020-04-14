using System;
using System.IO;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using Suits.Utility.Extensions;

namespace Suits.Jukebox.Models
{
    public class PlayableMedia : IMediaInfo
    {
        public PlayableMedia(Metadata meta, byte[] data)
        {
            this.Meta = meta;
            this.mediaData = data;
        }

        public PlayableMedia(PlayableMedia toCopy)
        {
            Meta = toCopy.Meta;
            mediaData = toCopy.mediaData;
        }

        private PlayableMedia(Metadata meta)
        {
            Meta = meta;
        }

        public static async Task<PlayableMedia> LoadFromFileAsync(string songPath)
        {
            songPath = Path.ChangeExtension(songPath, Metadata.MetaFileExtension);
            if (!File.Exists(songPath))
                throw new FileNotFoundException($"Metadata file was not found.");

            var meta = await Metadata.LoadMetadataFromFileAsync(songPath);
            if (!File.Exists(meta.MediaPath))
                throw new FileNotFoundException("The associated media file of this metadata does not exist.");
            return new PlayableMedia(meta);
        }

        public virtual Metadata Meta { get; protected set; }

        private byte[]? mediaData;

        protected string? saveDir;

        public TimeSpan GetDuration() => Meta.Info.Duration;
        public string GetTitle() => Meta.Info.Title;
        public string? GetThumbnail() => Meta.Info.Thumbnail;
        public string GetID() => throw new NotImplementedException();
        public string GetURL() => throw new NotImplementedException();

        protected virtual Task SaveDataAsync()
        {
            var location = Path.Combine(saveDir ?? throw new NullReferenceException("Please set saveDir before saving."), Meta.Info.Title.ReplaceIllegalCharacters() + Meta.FileExtension);
            if (mediaData == null) throw new Exception("Media data was null.");
            File.WriteAllBytes(location, mediaData);
            mediaData = null;
            Meta.MediaPath = location;
            return Task.CompletedTask;
        }
    }
}