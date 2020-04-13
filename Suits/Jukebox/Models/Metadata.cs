using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Runtime.Serialization;
using Suits.Jukebox.Services;
using Suits.Core.Services;

namespace Suits.Jukebox.Models
{
    [Serializable]
    public class Metadata
    {
        public Metadata(MediaInfo info, string fileFormat)
        {
            this.Info = info;
            FileFormat = fileFormat;
        }

        private static readonly BinarySerializer bs = new BinarySerializer();
        public static Task<Metadata> LoadMetadataFromFileAsync(string fullPath)
        {
            return bs.DeserializeFileAsync<Metadata>(fullPath);
        }

        public const string MetaFileExtension = ".meta";

        public string? MediaPath { get; set; }

        public string FileFormat { get; }
        public string FileExtension => FileFormat.Insert(0, ".");

        public MediaInfo Info { get; }
    }
}
