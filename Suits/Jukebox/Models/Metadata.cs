﻿using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Runtime.Serialization.Formatters.Binary;
using System.Runtime.Serialization;
using Suits.Jukebox.Services;

namespace Suits.Jukebox.Models
{
    [Serializable]
    public class Metadata
    {
        public Metadata(string title, string format, TimeSpan duration)
        {
            Title = title;
            Format = format;
            Duration = duration;
        }

        public static Task<Metadata> LoadMetadataFromFileAsync(string fullPath)
        {
            return Serializer.DeserializeFileAsync<Metadata>(fullPath);
        }     

        public const string MetaFileExtension = ".meta";

        public string MediaPath { get; set; }

        public string Title { get; }

        public string Extension { get => '.' + Format; }

        public string Format { get; }

        public TimeSpan Duration { get; }
    }
}