using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

using Melodica.Services.Downloaders;
using Melodica.Services.Downloaders.Spotify;
using Melodica.Services.Downloaders.YouTube;
using Melodica.Utility.Extensions;

namespace Melodica.Services.Services.Downloaders
{
    public class DownloaderProvider
    {
        static IEnumerable<AsyncDownloaderBase>? cachedSubtypes;

        private static IEnumerable<AsyncDownloaderBase> GetDownloaderSubTypesDynamically()
        {
            var asm = Assembly.GetExecutingAssembly();
            var types = asm.GetTypes();
            var subtypeInfoElems = types.Where(x => x.IsSubclassOf(typeof(AsyncDownloaderBase)));
            var subtypeInfoCount = subtypeInfoElems.Count();

            var subtypeObjs = new AsyncDownloaderBase[subtypeInfoCount];
            for (int i = 0; i < subtypeInfoCount; i++)
            {
                var subtypeInfo = subtypeInfoElems.ElementAt(i);
                var newObj = (AsyncDownloaderBase)(asm.CreateInstance(subtypeInfo.FullName ?? throw new Exception("Full name for downloader type was null.")) ?? throw new Exception("Could not create downloader type dynamically."));
                subtypeObjs[i] = newObj;
            }
            return subtypeObjs;
        }

        public AsyncDownloaderBase? GetDownloaderFromQuery(string query)
        {
            if(!query.IsUrl())
                return AsyncDownloaderBase.Default;

            cachedSubtypes ??= GetDownloaderSubTypesDynamically();
            foreach (var type in cachedSubtypes)
            {
                if (type.IsUrlSupported(query))
                    return type;
            }
            throw new Exception("URL is not supported.");

            //if (AsyncYoutubeDownloader.IsUrlSupported(url))
            //    return new AsyncYoutubeDownloader();
            //else if (AsyncSpotifyDownloader.IsUrlSupported(url))
            //    return new AsyncSpotifyDownloader();
            //else return null;
        }
    }
}
