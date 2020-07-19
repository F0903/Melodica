using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Suits.Jukebox.Models.Requests
{
    public struct SubRequestInfo
    {
        public MediaMetadata? ParentRequestInfo { get; set; }
    }

    public abstract class MediaRequest
    {
        public MediaRequest(PlayableMedia media)
        {
            RequestMediaType = MediaType.Video;
            this.media = media;
        }

        protected MediaRequest()
        {}

        public abstract MediaType RequestMediaType { get; protected set; }

        public abstract SubRequestInfo? SubRequestInfo { get; protected set; }

        public abstract List<MediaRequest>? SubRequests { get; set; }


        private readonly PlayableMedia? media;

        public virtual MediaMetadata GetInfo()
        {
            return media!.Info;
        }

        public virtual Task<PlayableMedia> GetMediaAsync()
        {
            return Task.FromResult(media!);
        }
    }
}
