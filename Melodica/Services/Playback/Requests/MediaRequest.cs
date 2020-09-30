using System.Collections.Generic;
using System.Threading.Tasks;

using Melodica.Services.Models;

namespace Melodica.Services.Playback.Requests
{
    public struct SubRequestInfo
    {
        public MediaMetadata ParentRequestInfo { get; set; }
    }

    public abstract class MediaRequest
    {
        public abstract SubRequestInfo? SubRequestInfo { get; protected set; }

        public abstract List<MediaRequest>? SubRequests { get; set; }

        public abstract MediaMetadata GetInfo();

        public abstract Task<PlayableMedia> GetMediaAsync();
    }
}
