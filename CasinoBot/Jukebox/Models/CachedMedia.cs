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
        public CachedMedia(PlayableMedia media, string saveDir, IFormatter formatter) : base(media)
        {
            this.saveDir = saveDir;
            this.formatter = formatter;
            SaveDataAsync().Wait();
        }

        private readonly IFormatter formatter;

        protected async override Task SaveDataAsync()
        {
            await base.SaveDataAsync();
            
            using var mediaMeta = new FileStream(Path.Combine(saveDir, Meta.Title.ReplaceIllegalCharacters() + Metadata.MetaFileExtension), FileMode.Create);
            formatter.Serialize(mediaMeta, Meta);
        }
    }
}
