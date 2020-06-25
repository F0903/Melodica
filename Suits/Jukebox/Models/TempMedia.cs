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
        public TempMedia(MediaMetadata meta, Stream data) : base(meta, data)
        {
            var toSave = Path.Combine(MediaCache.RootCacheLocation, "temp/");
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
