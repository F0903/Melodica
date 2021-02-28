using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Melodica.Services.Media
{
    public class MediaCollection
    {
        readonly MediaInfo? info;
        public MediaInfo CollectionInfo { get => info ?? media.First().Info; init => info = value; }

        readonly List<PlayableMedia> media = new List<PlayableMedia>();

        public IEnumerable<PlayableMedia> GetMedia() => media;

        public bool IsEmpty() => media.Count == 0;
    }
}
