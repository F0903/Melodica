using System;
using System.IO;
using System.Threading.Tasks;

using Melodica.Services.Caching;

namespace Melodica.Services.Media
{
    public sealed class TempMedia : PlayableMedia
    {
        public TempMedia(MediaInfo meta, DataGetter dataGetter) : base(meta, null, dataGetter, null)
        {
            throw new NotImplementedException();
            //TODO:
        }
    }
}