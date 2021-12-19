namespace Melodica.Services.Media;

public class StreamableMedia : PlayableMedia
{
    public StreamableMedia(MediaInfo info, string url, string format)
        : base(info, null, _ => Task.FromResult(new DataPair(null, new(format))), null)
    {
        dataInfo = new(format, url);
    }

    private readonly DataInfo dataInfo;

    public override Task<DataInfo> GetDataAsync()
    {
        return Task.FromResult(dataInfo);
    }
}
