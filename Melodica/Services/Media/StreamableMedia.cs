using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Melodica.Services.Media
{
    public class StreamableMedia : PlayableMedia
    {
        public StreamableMedia(MediaInfo info, string url, string format) 
            : base(info, null, _ => Task.FromResult(new DataPair(null, new(format, url))))
        {

        }

        public override Task<string> SaveDataAsync(string saveDir)
        {
            // Used for caching purposes, so just return empty string.
            return Task.FromResult("");
        }
    }
}
