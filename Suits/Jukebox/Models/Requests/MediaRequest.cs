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
            Requests.Add(this);
            for (int i = 1; i < col.Length; i++)
            {
                Requests.Add(new MediaRequest(col[i]));
            }
        }

        public MediaRequest(PlayableMedia media)
        {
            this.col = media;
        }

        protected MediaRequest() { }

        private readonly PlayableMedia? col;

        public bool IsPlaylist { get; protected set; } = false; 

        protected List<MediaRequest> Requests { get; set; } = new List<MediaRequest>();

        public virtual Task<IEnumerable<MediaRequest>> GetRequestsAsync()
        {
            return Task.FromResult((IEnumerable<MediaRequest>)Requests);
        }

        public virtual IMediaInfo GetMediaInfo()
        {
            return col!;
        }
     
        public virtual Task<PlayableMedia> GetMediaAsync()
        {
            return Task.FromResult(col!);
        }
    }
}
