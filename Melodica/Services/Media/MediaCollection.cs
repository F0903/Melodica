using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Melodica.Services.Media
{
    public class MediaCollection
    {
        public MediaCollection(IEnumerable<PlayableMedia> media, MediaInfo collectionInfo)
        {
            this.media = media;
            this.CollectionInfo = collectionInfo;
        }

        public MediaCollection(PlayableMedia media)
        {
            this.media = new[] { media };
        }

        public MediaInfo? CollectionInfo { get; init; }

        readonly IEnumerable<PlayableMedia> media;

        public IEnumerable<PlayableMedia> GetMedia() => media;

        public PlayableMedia First() => media.ElementAt(0);
    }
}
