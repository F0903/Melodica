using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Melodica.Core.Exceptions;
using Melodica.Services.Media;
using Melodica.Utility.Extensions;

namespace Melodica.Services.Playback.Requests
{
    //TODO:
    public class LocalMediaRequest : IMediaRequest
    {
        public LocalMediaRequest(string uri)
        {
            bool isFolder = uri.EndsWith('/') || uri.EndsWith('\\') || !uri.Contains('.');

            if (!isFolder)
            {
                if (!File.Exists(uri)) throw new CriticalException("File does not exist.");
                info = EvalInfo(new FileInfo(uri));
                return;
            }
            else if (!Directory.Exists(uri))
            {
                throw new CriticalException("Directory does not exist.");
            }
        }

        private static readonly string[] AllowedMediaExts =
        {
            ".mp3",
            ".mp4",
            ".wav"
        };

        private readonly MediaInfo info;

        private static MediaInfo EvalInfo(FileInfo file)
        {
            var meta = new MediaInfo()
            {
                Title = file.Name,
                Id = file.Name,
                MediaType = MediaType.Video
            };
            meta.DataInformation.Format = file.Extension.Remove(0, 1);
            meta.DataInformation.MediaPath = file.FullName;
            return meta;
        }

        public Task<MediaInfo> GetInfoAsync() => Task.FromResult(info);

        public Task<MediaCollection> GetMediaAsync() => Task.FromResult(new MediaCollection(new PlayableMedia(info, null, null)));
    }
}