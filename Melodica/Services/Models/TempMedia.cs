using System.IO;

using Melodica.Services.Caching;

namespace Melodica.Services.Models
{
    public sealed class TempMedia : PlayableMedia
    {
        public TempMedia(MediaMetadata meta, Stream data) : base(meta, data)
        {
            string? toSave = Path.Combine(MediaFileCache.RootCacheLocation, $"temp/{meta.Title}");
            string? toSaveDir = Path.GetDirectoryName(toSave);

            if (!Directory.Exists(toSaveDir))
                Directory.CreateDirectory(toSaveDir);

            saveLocation = toSave;
            SaveDataAsync(saveLocation).Wait();
        }

        ~TempMedia()
        {
            Destroy();
        }

        private readonly string saveLocation;

        private void Destroy() => File.Delete(saveLocation);
    }
}
