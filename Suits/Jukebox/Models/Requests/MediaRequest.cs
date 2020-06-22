using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Suits.Jukebox.Models.Requests
{
    public class MediaRequest
    {
        public MediaRequest(PlayableMedia media)
        {
            MediaType = MediaType.Video;
            this.media = media;
            SubRequests.Add(this);
        }

        protected MediaRequest()
        {}

        public MediaType MediaType { get; protected set; }

        protected List<MediaRequest> SubRequests { get; set; } = new List<MediaRequest>();

        private readonly PlayableMedia? media;

        /// <summary>
        /// Get the subrequests tied to this request. Will be more than one if a playlist is requested.
        /// </summary>
        /// <returns> Subrequests tied to the request. </returns>
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
