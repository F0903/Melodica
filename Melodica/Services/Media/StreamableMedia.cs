using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Melodica.Services.Caching;

namespace Melodica.Services.Media
{
    public class StreamableMedia : PlayableMedia
    {
        public StreamableMedia(MediaInfo info, string url, string format) 
            : base(info, null, _ => Task.FromResult(new DataPair(null, new(format))), null)
        {
            dataInfo = new(format, url);
        }

        private readonly DataInfo dataInfo;

        public override Task<DataInfo> SaveDataAsync()
        {
            return Task.FromResult(dataInfo);
        }
    }
}
