using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Melodica.Services.Caching;

// Currently unused. (maybe use for the old temporary media or just delete this)

internal class TemporaryFile : IDisposable
{
    public TemporaryFile(string path)
    {
        this.path = path;
    }

    private readonly string path;

    public void Dispose()
    {
        File.Delete(path);
    }
}

internal static class TemporaryFileCache
{
    const string BasePath = "./temp/";
    
    public static async Task<TemporaryFile> WriteStream(Stream stream)
    {
        var name = Guid.NewGuid().ToString();
        var path = Path.Combine(BasePath, name);
        using var fs = File.Create(path);
        await stream.CopyToAsync(fs);
        return new TemporaryFile(path);
    }
}
