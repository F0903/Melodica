using System.Collections.Generic;
using System.Threading.Tasks;

using Melodica.Services.Media;

namespace Melodica.Services.Playback.Requests
{
    public abstract class MediaRequest
    {
        public abstract MediaInfo? ParentRequestInfo { get; protected set; }

        public abstract List<MediaRequest>? SubRequests { get; set; }

        public abstract MediaInfo GetInfo();

        public abstract Task<PlayableMedia> GetMediaAsync();
    }
}