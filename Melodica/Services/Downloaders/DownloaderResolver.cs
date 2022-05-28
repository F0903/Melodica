using System.Reflection;

using Melodica.Services.Downloaders.Exceptions;
using Melodica.Utility.Extensions;

namespace Melodica.Services.Downloaders;

public static class DownloaderResolver
{
    private static IEnumerable<IAsyncDownloader>? cachedSubtypes;

    private static IEnumerable<IAsyncDownloader> GetDownloaderSubTypesDynamically()
    {
        var asm = Assembly.GetExecutingAssembly();
        var types = asm.GetTypes();
        var subtypeInfoElems = types.Where(x => x.GetInterface(nameof(IAsyncDownloader)) != null);
        int subtypeInfoCount = subtypeInfoElems.Count();

        var subtypeObjs = new IAsyncDownloader[subtypeInfoCount];
        for (int i = 0; i < subtypeInfoCount; i++)
        {
            var subtypeInfo = subtypeInfoElems.ElementAt(i);
            var newObj = (IAsyncDownloader)(asm.CreateInstance(subtypeInfo.FullName ?? throw new Exception("Full name for downloader type was null.")) ?? throw new Exception("Could not create downloader type dynamically."));
            subtypeObjs[i] = newObj;
        }
        return subtypeObjs;
    }

    public static IAsyncDownloader GetDownloaderFromQuery(string query)
    {
        if (!query.IsUrl())
            return IAsyncDownloader.Default;

        cachedSubtypes ??= GetDownloaderSubTypesDynamically();
        foreach (IAsyncDownloader type in cachedSubtypes)
        {
            if (type.IsUrlSupported(query))
                return type;
        }
        throw new UnrecognizedUrlException("URL is not recognized as a supported url.");
    }
}
