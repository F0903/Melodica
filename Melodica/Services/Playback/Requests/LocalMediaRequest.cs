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
    public class LocalMediaRequest : MediaRequest
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

            string? dirName = Path.GetDirectoryName(uri);
            if (dirName == null)
                throw new Exception("Directory name returned null.");
            info = new MediaMetadata()
            {
                Id = dirName,
                Title = dirName,
                MediaType = MediaType.Playlist
            };

            SubRequests = new List<MediaRequest>();
            foreach (var file in Directory.EnumerateFiles(uri).Convert(x => new FileInfo(x)))
            {
                if (!AllowedMediaExts.Any(x => x == file.Extension))
                    return;

                SubRequests.Add(new LocalMediaRequest(file, info));
            }
        }

        public override MediaMetadata? ParentRequestInfo { get; protected set; }
        public override List<MediaRequest>? SubRequests { get; set; }

        private LocalMediaRequest(FileInfo file, MediaMetadata parentMeta)
        {
            info = EvalInfo(file);
            ParentRequestInfo = parentMeta;
        }

        private static readonly string[] AllowedMediaExts =
        {
            ".mp3",
            ".mp4",
            ".wav"
        };

        private readonly MediaMetadata info;

        private static MediaMetadata EvalInfo(FileInfo file)
        {
            var meta = new MediaMetadata()
            {
                Title = file.Name,
                Id = file.Name,
                MediaType = MediaType.Video
            };
            meta.DataInformation.Format = file.Extension.Remove(0, 1);
            meta.DataInformation.MediaPath = file.FullName;
            return meta;
        }

        public override MediaMetadata GetInfo() => info;

        public override Task<PlayableMedia> GetMediaAsync() => Task.FromResult(new PlayableMedia(info, null));
    }
}