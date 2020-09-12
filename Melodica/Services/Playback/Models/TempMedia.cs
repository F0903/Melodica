﻿using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text;
using Melodica.Services.Services;
using System.IO;

namespace Melodica.Services.Playback.Models
{
    public sealed class TempMedia : PlayableMedia
    {
        public TempMedia(MediaMetadata meta, Stream data) : base(meta, data)
        {
            var toSave = Path.Combine(MediaFileCache.RootCacheLocation, $"temp/{meta.Title}");
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
