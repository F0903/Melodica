
using Discord;
using Discord.Interactions;

using Melodica.Services.Playback;

namespace Melodica.Services.Wiki;

public sealed class WikiCommands : InteractionModuleBase<SocketInteractionContext>
{
    public WikiCommands(IWikiProvider wiki)
    {
        this.wiki = wiki;
    }

    private readonly IWikiProvider wiki;

    private Jukebox GetJukebox => JukeboxManager.GetJukebox(Context.Guild);

    async Task<WikiElement?> GetPlayingInfoAsync()
    {
        Jukebox juke;
        try { juke = GetJukebox; }
        catch
        {
            await ReplyAsync("You must have a song playing when using this command with no parameter.");
            return null;
        }
        if (!juke.Playing)
        {
            await ReplyAsync("You must have a song playing when using this command with no parameter.");
            return null;
        }

        Media.PlayableMedia? song = juke.GetSong();
        if (song is null)
            throw new NullReferenceException("Song was null.");
        string? artist = song.Info.Artist;

        return await wiki.GetInfoAsync(artist);
    }

    [SlashCommand("wiki", "Gets info from a wiki for the specified page.")]
    public async Task InfoAsync(string? pageTitle = null)
    {
        await DeferAsync();

        WikiElement? maybeInfo = pageTitle is null ? await GetPlayingInfoAsync() : await wiki.GetInfoAsync(pageTitle!);
        if (maybeInfo is null)
        {
            await ModifyOriginalResponseAsync(x => x.Content = "No page title was provided, and no song is currently playing.");
            return;
        }
        WikiElement info = maybeInfo.Value;

        await ModifyOriginalResponseAsync(x => x.Embed = new EmbedBuilder()
        {
            ThumbnailUrl = info.ImageUrl,
            Title = info.Title,
            Description = info.Info
        }.Build());
    }
}
