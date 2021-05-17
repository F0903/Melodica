using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Melodica.Services.Media
{
    public class MediaCollection : IEnumerable<LazyMedia>
    {
        public MediaCollection(IEnumerable<LazyMedia> media, MediaInfo collectionInfo)
        {
            this.media = media;
            this.CollectionInfo = collectionInfo;
        }

        public MediaCollection(LazyMedia media)
        {
            this.media = new LazyMedia[] { new(media) };
        }

        public MediaInfo? CollectionInfo { get; init; }

        readonly IEnumerable<LazyMedia> media;

        public IEnumerator<LazyMedia> GetEnumerator()
        {
            foreach (var item in media)
            {
                yield return item;
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
