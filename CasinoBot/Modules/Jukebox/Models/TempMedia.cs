using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text;
using CasinoBot.Modules.Jukebox.Services.Cache;
using System.IO;

namespace CasinoBot.Modules.Jukebox.Models
{
    public class TempMedia : PlayableMedia
    {
        public TempMedia(Metadata meta, byte[] data, MediaCache cache) : base(meta, data)
        {
            var toSave = Path.Combine(cache.localCache, "temp/");
            if (!Directory.Exists(toSave))
                Directory.CreateDirectory(toSave);
            saveDir = toSave;
            SaveDataAsync().Wait();
        }

        ~TempMedia()
        {
            File.Delete(Meta.MediaPath);
        }

        protected override Task SaveDataAsync()
        {
            return base.SaveDataAsync();
        }
    }
}
