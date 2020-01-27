﻿using System;
using System.IO;
using System.Threading.Tasks;
using CasinoBot.Utility.Extensions;

namespace CasinoBot.Modules.Jukebox.Models
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

        public static async Task<PlayableMedia> LoadFromFileAsync(string pathToMetadata)
        {
            if (!File.Exists(pathToMetadata))
                throw new FileNotFoundException($"Metadata file was not found.");

            var meta = await Metadata.LoadMetadataFromFileAsync(pathToMetadata);
            if (!File.Exists(meta.MediaPath))
                throw new FileNotFoundException("The associated media file of this metadata does not exist.");
            return new PlayableMedia(meta);
        }

        public Metadata Meta { get; protected set; }

        private readonly byte[] mediaData;

        protected string saveDir;

        public TimeSpan GetDuration() => Meta.Duration;
        public string GetTitle() => Meta.Title;

        protected virtual Task SaveDataAsync()
        {
            var location = Path.Combine(saveDir, Meta.Title.ReplaceIllegalCharacters() + Meta.Extension);
            File.WriteAllBytes(location, mediaData);
            Meta.MediaPath = location;
            return Task.CompletedTask;
        }
    }
}