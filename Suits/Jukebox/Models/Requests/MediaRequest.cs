using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Suits.Jukebox.Models.Requests
{
    public struct SubRequestInfo
    {
        public bool IsSubRequest { get; set; }
        public MediaMetadata? ParentRequestInfo { get; set; }
    }

    public class MediaRequest
    {
        public MediaRequest(PlayableMedia media)
        {
            RequestMediaType = MediaType.Video;
            this.media = media;
        }

        protected MediaRequest()
        {}

        public MediaType RequestMediaType { get; protected set; }

        public SubRequestInfo SubRequestInfo { get; protected set; }

        protected List<MediaRequest> SubRequests { get; set; } = new List<MediaRequest>();

        private readonly PlayableMedia? media;

        public virtual Task<IEnumerable<MediaRequest>> GetSubRequestsAsync()
        {
            return Task.FromResult((IEnumerable<MediaRequest>)SubRequests);
        }

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
