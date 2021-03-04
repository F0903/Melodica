using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Melodica.Services.Media
{
    public class MediaCollection
    {
        public MediaCollection(IEnumerable<PlayableMedia> media)
        {
            this.media = media;
        }

        public MediaCollection(PlayableMedia media)
        {
            this.media = new[] { media };
        }

        readonly MediaInfo? info;
        public MediaInfo CollectionInfo { get => info ?? media.First().Info; init => info = value; }

        readonly IEnumerable<PlayableMedia> media;

        public IEnumerable<PlayableMedia> GetMedia() => media;

        public PlayableMedia First() => media.ElementAt(0);
    }
}
