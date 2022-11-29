using Discord;
using Discord.Interactions;

using Melodica.Services.Playback;

namespace Melodica.Services.Lyrics;

public sealed class LyricsCommands : InteractionModuleBase<SocketInteractionContext>
{
    public LyricsCommands(ILyricsProvider lyrics)
    {
        this.lyrics = lyrics;
    }

    private readonly ILyricsProvider lyrics;

    [SlashCommand("lyrics", "Gets lyrics for a search term.")]
    public async Task GetLyrics(string? songName = null)
    {
        Jukebox? juke;
        try { juke = JukeboxManager.GetJukebox(Context.Guild); }
        catch (Exception) { juke = null; }

        if (songName is null && !(juke is not null && juke.Playing))
        {
            await RespondAsync("You either need to specify a search term or have a song playing.");
            return;
        }

        await DeferAsync();

        if (songName is null)
        {
            var song = juke!.GetSong();
            if (song is null)
                throw new NullReferenceException("Song was null. (dbg-err)");

            var songInfo = song.Info;
            songName = $"{songInfo.Artist} {songInfo.Title}";
        }

        LyricsInfo lyrs;
        try
        {
            lyrs = await lyrics.GetLyricsAsync(songName);
        }
        catch (LyricsNotFoundException ex)
        {
            await ModifyOriginalResponseAsync(x => x.Content = ex.Message);
            return;
        }

        var text = lyrs.Lyrics.AsMemory();
        const int pageSize = 2048;
        for (var i = 0; i < text.Length; i += pageSize)
        {
            var pageText = text.Span.Slice(i, Math.Min(pageSize, text.Length - i)).ToString();
            await ModifyOriginalResponseAsync(x => x.Embed = new EmbedBuilder()
            {
                Title = i == 0 ? lyrs.Title : "",
                ThumbnailUrl = i == 0 ? lyrs.Image : "",
                Description = pageText
            }.Build());
        }
    }
}
