using System.Collections.Generic;
using System.Threading.Tasks;

using Melodica.Services.Media;

namespace Melodica.Services.Playback.Requests
{
    public interface IMediaRequest
    {
        public Task<MediaInfo> GetInfoAsync();

        public Task<MediaCollection> GetMediaAsync();
    }
}