using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Suits.Jukebox.Models.Requests
{
    public struct SubRequestInfo
    {
        public MediaMetadata ParentRequestInfo { get; set; }
    }

    public abstract class MediaRequestBase
    {
        public abstract MediaType RequestMediaType { get; protected set; }

        public abstract SubRequestInfo? SubRequestInfo { get; protected set; }

        public abstract List<MediaRequestBase>? SubRequests { get; set; }

        public abstract MediaMetadata GetInfo();

        public abstract Task<PlayableMedia> GetMediaAsync();
    }
}
