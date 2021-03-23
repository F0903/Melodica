using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Melodica.Services.Media;

namespace Melodica.Services.Caching
{
    public interface IMediaCache
    {
        public Task<PlayableMedia> GetAsync(string id);

        public bool TryGet(string id, out PlayableMedia? media);

        public Task<DataInfo> CacheAsync(PlayableMedia med, DataPair data, bool pruneCache = true);
    }
}
