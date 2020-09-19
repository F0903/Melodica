using System.Collections.Generic;
using System.Threading.Tasks;

using Melodica.Services.Models;

namespace Melodica.Services.Playback.Requests
{
    public struct SubRequestInfo
    {
        public MediaMetadata ParentRequestInfo { get; set; }
    }

    public abstract class MediaRequestBase
    {
        public abstract SubRequestInfo? SubRequestInfo { get; protected set; }

        public abstract List<MediaRequestBase>? SubRequests { get; set; }

        public abstract MediaMetadata GetInfo();

        public abstract Task<PlayableMedia> GetMediaAsync();
    }
}
