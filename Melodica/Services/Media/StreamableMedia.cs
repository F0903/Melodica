namespace Melodica.Services.Media;

public sealed class StreamableMedia(MediaInfo info, string url, string format)
    : PlayableMedia(info, null, _ => Task.FromResult(new DataPair(null, new(format))), null)
{
    private readonly DataInfo dataInfo = new(format, url);

    public override Task<DataInfo> GetDataAsync() => Task.FromResult(dataInfo);
}
