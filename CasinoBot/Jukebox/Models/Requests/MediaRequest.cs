using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace CasinoBot.Jukebox.Models.Requests
{
    public class MediaRequest
    {
        public MediaRequest(MediaCollection col)
        {
            this.col = col;
        }

        public MediaRequest(PlayableMedia media)
        {
            this.col = new MediaCollection(media);
        }

        protected MediaRequest() { }

        private readonly MediaCollection col;

        public virtual Task<MediaCollection> GetMediaRequestAsync()
        {
            return Task.FromResult(col);
        }
    }
}
