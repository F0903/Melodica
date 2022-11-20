using System.Linq;

using Discord;
using Discord.Interactions;
using Discord.WebSocket;

using Melodica.Services.Caching;
using Melodica.Services.Media;
using Melodica.Services.Playback.Exceptions;
using Melodica.Services.Playback.Requests;

namespace Melodica.Services.Playback;

public sealed class JukeboxInteractionCommands : InteractionModuleBase<SocketInteractionContext>
{
    Jukebox? cachedJukebox;
    private Jukebox Jukebox => cachedJukebox ??=
        JukeboxManager.GetOrCreateJukebox(Context.Guild, () => new Jukebox(Context.Channel));

    [SlashCommand("clear-cache", "Clears the media cache.")]
    public async Task ClearCache()
    {
        (int deletedFiles, int filesInUse, long ms) = await MediaFileCache.ClearAllCachesAsync();
        await RespondAsync($"Deleted {deletedFiles} files. ({filesInUse} files in use) [{ms}ms]", ephemeral: true);
    }

    [SlashCommand("duration", "Shows the remaining duration of the current song.")]
    public async Task Duration()
    {
        if (!Jukebox.Playing)
        {
            await RespondAsync("No song is playing :(", ephemeral: true);
            return;
        }

        TimeSpan dur = Jukebox.Elapsed;
        PlayableMedia? song = Jukebox.GetSong();
        if (song is null)
        {
            await RespondAsync("Could not get song from jukebox. Contact developer.", ephemeral: true);
            return;
        }
        TimeSpan songDur = song.Info.Duration;
        await RespondAsync((songDur != TimeSpan.Zero ? $"__{songDur}__\n" : "") + $"{dur}", ephemeral: true);
    }

    [SlashCommand("clear", "Clears queue.")]
    public async Task Clear()
    {
        await Jukebox.ClearAsync();
        await RespondAsync("Cleared queue.", ephemeral: true);
    }

    [SlashCommand("remove", "Removes song from queue by index, or removes the last element if no parameter is given.")]
    public async Task Remove(int? index = null)
    {
        var queue = Jukebox.GetQueue();
        var removed = index == null ? await queue.RemoveAtAsync(^0) : await queue.RemoveAtAsync(index.Value - 1);
        var removedInfo = removed.Info;
        await RespondAsync(embed: new EmbedBuilder()
        {
            Title = "**Removed**",
            Description = removedInfo.Title
        }.Build(),
        ephemeral: true
        );
    }

    [SlashCommand("queue", "Shows the current queue.")]
    public async Task Queue()
    {
        await DeferAsync(true);
        var queue = Jukebox.GetQueue();
        var eb = new EmbedBuilder();
        if (queue.IsEmpty)
        {
            eb.WithTitle("**Queue**")
              .WithDescription("No songs are queued.")
              .WithFooter("It's quite empty down here...");
        }
        else
        {
            (TimeSpan queueDuration, string? imageUrl) = await queue.GetQueueInfo();
            eb.WithTitle("**Queue**")
              .WithThumbnailUrl(imageUrl)
              .WithFooter($"{(queueDuration == TimeSpan.Zero ? '\u221E'.ToString() : queueDuration.ToString())}{(Jukebox.Shuffle ? " | Shuffle" : "")}");

            int maxElems = 20;
            for (int i = 1; i <= maxElems; i++)
            {
                if (i > queue.Length)
                    break;
                var song = queue[i - 1];
                var songInfo = song.Info;
                eb.AddField(
                    i == 1 ? "Next:" : i == maxElems ? "And more" : i.ToString(),
                    i == 1 ? $"**{songInfo.Artist} - {songInfo.Title}**" : i == maxElems ? $"Plus {queue.Length - (i - 1)} other songs!" : $"{songInfo.Artist} - {songInfo.Title}",
                    false);
            }
        }

        await ModifyOriginalResponseAsync(x => x.Embed = eb.Build());
    }

    [SlashCommand("next", "Sets the next song to play.")]
    public async Task Next(string query)
    {
        var downloader = Downloaders.DownloaderResolver.GetDownloaderFromQuery(query);
        var request = new DownloadRequest(query.AsMemory(), downloader);

        // Get info to see if the request is actually valid.
        var info = await request.GetInfoAsync();

        var jukebox = Jukebox;
        await jukebox.SetShuffle(false);
        await jukebox.SetNextAsync(request);
        await RespondAsync(embed: EmbedUtils.CreateMediaEmbed(info, null), ephemeral: true);
    }

    Task<(IVoiceChannel?, IMediaRequest)> GetPlaybackContext(string query)
    {
        var voice = (Context.User as SocketGuildUser)?.VoiceChannel;
        var downloader = Downloaders.DownloaderResolver.GetDownloaderFromQuery(query);
        var request = new DownloadRequest(query.AsMemory(), downloader);
        return Task.FromResult(((IVoiceChannel?)voice, (IMediaRequest)request));
    }

    [SlashCommand("switch", "Switches the current song to the one specified.")]
    public async Task Switch(string query)
    {
        var jukebox = Jukebox;
        if (!jukebox.Playing)
        {
            await RespondAsync("Switch only works when a song is playing!", ephemeral: true);
            return;
        }

        if (query is null)
        {
            await RespondAsync("You need to specify a url or search query.", ephemeral: true);
            return;
        }

        await DeferAsync();

        var (_, request) = await GetPlaybackContext(query);
        try
        {
            //TODO: Switch doesn't seem to work correctly when playing a playlist
            await jukebox.SwitchAsync(request);
            await DeleteOriginalResponseAsync();
        }
        catch (EmptyChannelException) { await ModifyOriginalResponseAsync(x => x.Content = "All users have left the channel. Disconnecting..."); }
    }

    [SlashCommand("play", "Starts playing a song.")]
    public async Task Play(string query)
    {  
        if (query is null)
        {
            await RespondAsync("You need to specify a url, search query or upload a file.", ephemeral: true);
            return;
        }
 
        try
        {
            var (voice, request) = await GetPlaybackContext(query);
            if (voice is null)
            {
                await RespondAsync("You need to be in a voice channel!", ephemeral: true);
                return;
            }

            if (!GuildPermissionsChecker.CheckVoicePermission(Context.Guild, Context.Client.CurrentUser, voice))
            {
                await RespondAsync("I don't have permission to connect and speak in this channel :(", ephemeral: true);
                return;
            }

            var jukebox = Jukebox;
            if (jukebox.Playing)
            {
                try
                {
                    var info = await request.GetInfoAsync();
                    var embed = EmbedUtils.CreateMediaEmbed(info, null);
                    await RespondAsync("The following media will be queued:", embed: embed, ephemeral: true);
                }
                catch (Exception ex)
                {
                    await RespondAsync($"Error occured getting media info: {ex}");
                    return;
                }
            }
            else
            {
                await DeferAsync(); // Command can take a long time.
            }

            var player = new Player(Context.Interaction);
            var result = await jukebox.PlayAsync(request, voice, player);
            switch (result)
            { 
                case Jukebox.PlayResult.Error:
                    await ModifyOriginalResponseAsync(x => x.Content = "An error occurred playing the media.");
                    break;
                default:
                    break;
            } 
        }
        catch (EmptyChannelException)
        {
            await ModifyOriginalResponseAsync(x => x.Content = "All users have left the channel. Disconnecting...");
        }
        catch (Exception ex)
        {
            await ModifyOriginalResponseAsync(x => x.Content = $"Error: {ex.Message}");
        }
    }

    [SlashCommand("abort", "Force the player to stop if the buttons aren't working.")]
    public async Task Abort()
    { 
        await RespondAsync("Stopping...", ephemeral: true);
        await Jukebox.StopAsync();
    }

    [ComponentInteraction("player_stop")]
    public async Task Stop()
    {
        if (!Jukebox.Playing)
        {
            await RespondAsync("No song is playing.", ephemeral: true);
            return;
        }

        await DeferAsync();
        var jukebox = Jukebox;
        await jukebox.StopAsync(); 
    }

    [ComponentInteraction("player_shuffle")]
    public async Task Shuffle()
    {
        await DeferAsync();
        var jukebox = Jukebox;
        await jukebox.SetShuffle(!jukebox.Shuffle);
    }

    [ComponentInteraction("player_repeat")]
    public async Task Repeat()
    {
        await DeferAsync();
        var jukebox = Jukebox;
        await jukebox.SetRepeat(!jukebox.Repeat);
    }

    [ComponentInteraction("player_loop")]
    public async Task Loop()
    {
        await DeferAsync();
        var jukebox = Jukebox;
        await jukebox.SetLoop(!jukebox.Loop);
    }

    [ComponentInteraction("player_togglepause")]
    public async Task TogglePause()
    {
        await DeferAsync();
        var jukebox = Jukebox;
        await jukebox.SetPaused(!jukebox.Paused);
    }

    [ComponentInteraction("player_skip")]
    public async Task Skip()
    {
        await DeferAsync();
        Jukebox.Skip();
    }
}
