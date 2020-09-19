using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Melodica.Core.Exceptions;
using Melodica.Services.Models;
using Melodica.Utility.Extensions;

namespace Melodica.Services.Playback.Requests
{
    public class LocalMediaRequest : MediaRequestBase
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
            else if (!Directory.Exists(uri)) throw new CriticalException("Directory does not exist.");

            var dirName = GetDirName(uri);
            info = new MediaMetadata()
            {
                ID = dirName,
                Title = dirName,
                MediaType = MediaType.Playlist
            };

            SubRequests = new List<MediaRequestBase>();
            foreach (var file in Directory.EnumerateFiles(uri).Convert(x => new FileInfo(x)))
            {
                if (!AllowedMediaExts.Any(x => x == file.Extension))
                    return;

                SubRequests.Add(new LocalMediaRequest(file, info));
            }
        }

        public override SubRequestInfo? SubRequestInfo { get; protected set; }
        public override List<MediaRequestBase>? SubRequests { get; set; }

        private LocalMediaRequest(FileInfo file, MediaMetadata parentMeta)
        {
            info = EvalInfo(file);
            SubRequestInfo = new SubRequestInfo() { ParentRequestInfo = parentMeta };
        }

        private static readonly string[] AllowedMediaExts =
        {
            ".mp3",
            ".mp4",
            ".wav"
        };

        readonly MediaMetadata info;

        private string GetDirName(string name)
        {
            bool forwardSlash = name.Contains('/');
            bool endingSlash = forwardSlash ? name[^1] == '/' : name[^1] == '\\';

            int start, end;
            if (endingSlash)
            {
                var altName = name.Remove(name.Length - 1, 1);
                start = forwardSlash ? altName.LastIndexOf('/') + 1 : altName.LastIndexOf('\\') + 1;
                end = forwardSlash ? name.LastIndexOf('/') : name.LastIndexOf('\\');
            }
            else
            {
                start = forwardSlash ? name.LastIndexOf('/') + 1 : name.LastIndexOf('\\') + 1;
                end = name.Length;
            }
            return name[start..end];
        }

        private MediaMetadata EvalInfo(FileInfo file)
        {
            var meta = new MediaMetadata()
            {
                Title = file.Name,
                ID = file.Name,
                MediaType = MediaType.Video
            };
            meta.DataInformation.Format = file.Extension.Remove(0, 1);
            meta.DataInformation.MediaPath = file.FullName;
            return meta;
        }

        public override MediaMetadata GetInfo()
        {
            return info;
        }

        public override Task<PlayableMedia> GetMediaAsync()
        {
            return Task.FromResult(new PlayableMedia(info, null));
        }
    }
}
