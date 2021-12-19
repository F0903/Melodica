namespace Melodica.Services.Lyrics;

public struct LyricsInfo
{
    public string Title { get; set; }

    public string Image { get; set; }

    public string Lyrics { get; set; }
}

public interface ILyricsProvider
{
    public Task<LyricsInfo> GetLyricsAsync(string input);
}
