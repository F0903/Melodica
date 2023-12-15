namespace Melodica.Services.Media;

public sealed class TempMedia : PlayableMedia
{
    public TempMedia(MediaInfo info, DataGetter data) : base(info, null, data, null) => this.data = data;

    readonly DataGetter data;
    string? path;

    public override async Task<DataInfo> GetDataAsync()
    {
        var pair = await data(this);
        if (pair.Data is null)
            throw new NullReferenceException("Data in temp media was null.");
        path = $"./temp/{Info.Title}";
        Directory.CreateDirectory("./temp");
        using var fs = File.OpenWrite(path);
        using (pair.Data) await pair.Data.CopyToAsync(fs);
        return new DataInfo(pair.Format, path);
    }

    public void DiscardTempMedia()
    {
        if (path is null)
            return;
        File.Delete(path);
    }
}
