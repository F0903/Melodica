using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Collections.Generic;
using System.Text;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using CasinoBot.Utility.Extensions;

namespace CasinoBot.Jukebox.Models
{  
    public sealed class CachedMedia : PlayableMedia
    {
        public CachedMedia(PlayableMedia media, string saveDir) : base(media)
        {
            this.saveDir = saveDir;
            SaveDataAsync().Wait();
        }        

        private readonly BinaryFormatter bin = new BinaryFormatter(null, new StreamingContext(StreamingContextStates.File));
       
        protected async override Task SaveDataAsync()
        {
            await base.SaveDataAsync();

            using var mediaMeta = new FileStream(Path.Combine(saveDir, Meta.Title.ReplaceIllegalCharacters() + Metadata.MetaFileExtension), FileMode.Create);
            bin.Serialize(mediaMeta, Meta);
        }
    }
}
