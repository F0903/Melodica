﻿using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using Suits.Utility.Extensions;
using Suits.Jukebox.Services;

namespace Suits.Jukebox.Models
{  
    public sealed class CachedMedia : PlayableMedia
    {
        public CachedMedia(PlayableMedia media, string saveDir) : base(media)
        {
            this.saveDir = saveDir;
            SaveDataAsync().Wait();
        }

        protected async override Task SaveDataAsync()
        {
            await base.SaveDataAsync();
            
            await Serializer.SerializeToFileAsync(Path.Combine(saveDir, Meta.Title.ReplaceIllegalCharacters() + Metadata.MetaFileExtension), Meta);
        }
    }
}