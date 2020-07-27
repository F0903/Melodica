using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text;
using Melodica.Jukebox.Services;
using System.IO;

namespace Melodica.Jukebox.Models
{
    public sealed class TempMedia : PlayableMedia
    {
        public TempMedia(MediaMetadata meta, Stream data) : base(meta, data)
        {
            var toSave = Path.Combine(MediaCache.RootCacheLocation, $"temp/{meta.Title}");
            var toSaveDir = Path.GetDirectoryName(toSave);

            if (!Directory.Exists(toSaveDir))
                Directory.CreateDirectory(toSaveDir);

            saveLocation = toSave;
            SaveDataAsync(saveLocation).Wait();
        }

        ~TempMedia()
        {
            Destroy();
        }

        readonly string saveLocation;

        private void Destroy()
        {
            File.Delete(saveLocation);
        }
    }
}
