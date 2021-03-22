﻿using System;
using System.IO;
using System.Threading.Tasks;

using Melodica.Services.Caching;

namespace Melodica.Services.Media
{
    public sealed class TempMedia : PlayableMedia
    {
        public TempMedia(MediaInfo meta, DataGetter dataGetter) : base(meta, null, dataGetter)
        {
            string toSave = Path.Combine(MediaFileCache.RootCacheLocation, "temp/");
            string? toSaveDir = Path.GetDirectoryName(toSave);
            if (toSaveDir == null)
                throw new NullReferenceException("toSaveDir was null for temp media. Wrong path probably specified.");

            if (!Directory.Exists(toSaveDir))
                Directory.CreateDirectory(toSaveDir);

            fullSavePath = toSave;
            fullSavePath = SaveDataAsync(fullSavePath).Result;
        }

        ~TempMedia()
        {
            Destroy();
        }

        private readonly string fullSavePath;

        private void Destroy() => File.Delete(fullSavePath);
    }
}