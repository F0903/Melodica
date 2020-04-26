using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Suits.Jukebox.Models.Requests
{
    public class MediaRequest
    {
        public MediaRequest(MediaCollection col)
        {
            Type = MediaType.Playlist;
            for (int i = 0; i < col.Length; i++)
            {
                Requests.Add(new MediaRequest(col[i]));
            }
        }

        public MediaRequest(PlayableMedia media)
        {
            Type = MediaType.Video;
            this.media = media;
            Requests.Add(this);
        }

        protected MediaRequest(bool addToRequests = true)
        {
            if (addToRequests)
                Requests.Add(this);
        }

        public MediaType Type { get; protected set; }

        protected List<MediaRequest> Requests { get; set; } = new List<MediaRequest>();

        private readonly PlayableMedia? media;

        public virtual Task<IEnumerable<MediaRequest>> GetRequestsAsync()
        {
            return Task.FromResult((IEnumerable<MediaRequest>)Requests);
        }

        public virtual Metadata GetMediaInfo()
        {
            return media!.Info;
        }

        public virtual Task<PlayableMedia> GetMediaAsync()
        {
            return Task.FromResult(media!);
        }
    }
}
