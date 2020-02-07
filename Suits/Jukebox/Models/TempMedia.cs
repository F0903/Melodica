using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text;
using Suits.Jukebox.Services.Cache;
using System.IO;

namespace Suits.Jukebox.Models
{
    public sealed class TempMedia : PlayableMedia
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
            Destroy();
        }

        private void Destroy()
        {
            File.Delete(saveDir);
        }

        protected override Task SaveDataAsync()
        {
            return base.SaveDataAsync();
        }
    }
}
