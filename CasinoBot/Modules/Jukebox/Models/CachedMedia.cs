using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Collections.Generic;
using System.Text;
using System.Runtime.Serialization;
using System.Threading.Tasks;

namespace CasinoBot.Modules.Jukebox.Models
{  
    public sealed class CachedMedia : PlayableMedia
    {
        public CachedMedia(Metadata meta, string saveDir) : base(meta)
        {
            SaveAsync(saveDir).Wait();
        }

        public CachedMedia(PlayableMedia media, string saveDir) : base(media)
        {
            SaveAsync(saveDir).Wait();
        }

        public const string MetaFileExtension = ".mmeta";

        private readonly BinaryFormatter bin = new BinaryFormatter(null, new StreamingContext(StreamingContextStates.File));

        private string metaPath;

        public static Task<Metadata> GetMetadataFromFile(string fullPath)
        {
            var formatter = new BinaryFormatter();
            return Task.FromResult((Metadata)formatter.Deserialize(File.OpenRead(fullPath)));
        }

        private async Task SaveAsync(string dir)
        {
            var mediaPath = Path.Combine(dir, Meta.Title + Meta.Extension);
            using var media = File.Create(mediaPath);
            var data = Meta.MediaData();
            await media.WriteAsync(data, 0, data.Length);
            Meta.SetPath(mediaPath);

            metaPath = Path.Combine(dir, Meta.Title + MetaFileExtension);

            using var mediaMeta = new FileStream(metaPath, FileMode.Create);
            bin.Serialize(mediaMeta, Meta);
        }
    }
}
