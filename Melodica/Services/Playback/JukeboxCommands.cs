using Discord;
using Discord.Interactions;
using Discord.WebSocket;

using Melodica.Services.Caching;
using Melodica.Services.Downloaders;
using Melodica.Services.Playback.Exceptions;
using Melodica.Services.Playback.Requests;

namespace Melodica.Services.Playback;

public enum ManualProviderOptions
{
    YouTube,
    Spotify,
    SoundCloud,
}

public sealed class JukeboxCommands : InteractionModuleBase<SocketInteractionContext>
{
    Jukebox? cachedJukebox;
    private Jukebox Jukebox => cachedJukebox ??=
        JukeboxManager.GetOrCreateJukebox(Context.Guild, () => new Jukebox(Context.Channel));

    [SlashCommand("clear-cache", "Clears the media cache.")]
    public async Task ClearCache()
    {
        try
        {
            (var deletedFiles, var filesInUse, var ms) = await MediaFileCache.ClearAllCachesAsync();
            await RespondAsync($"Deleted {deletedFiles} files. ({filesInUse} files in use) [{ms}ms]", ephemeral: true);
        }
        catch (NoMediaFileCachesException e)
        {
            await RespondAsync(e.Message, ephemeral: true); 
        } 
    }

    [SlashCommand("duration", "Shows the remaining duration of the current song.")]
    public async Task Duration()
    {
        if (!Jukebox.Playing)
        {
            await RespondAsync("No song is playing :(", ephemeral: true);
            return;
        }

        var dur = Jukebox.Elapsed;
        var song = Jukebox.GetSong();
        if (song is null)
        {
            await RespondAsync("Could not get song from jukebox. Contact developer.", ephemeral: true);
            return;
        }
        var songDur = song.Info.Duration;
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
        EmbedBuilder eb = new();
        if (queue.IsEmpty)
        {
            eb.WithTitle("**Queue**")
              .WithDescription("No songs are queued.")
              .WithFooter("It's quite empty down here...");
        }
        else
        {
            (var queueDuration, var imageUrl) = await queue.GetQueueInfo();
            eb.WithTitle("**Queue**")
              .WithThumbnailUrl(imageUrl)
              .WithFooter($"{(queueDuration == TimeSpan.Zero ? '\u221E'.ToString() : queueDuration.ToString())}{(Jukebox.Shuffle ? " | Shuffle" : "")}");

            var maxElems = 20;
            for (var i = 1; i <= maxElems; i++)
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
        var downloader = Downloader.GetFromQuery(query);
        DownloadRequest request = new(query.AsMemory(), downloader);

        // Get info to see if the request is actually valid.
        var info = await request.GetInfoAsync();

        var jukebox = Jukebox;
        await jukebox.SetShuffle(false);
        await jukebox.SetNextAsync(request);
        await RespondAsync(embed: EmbedUtils.CreateMediaEmbed(info, null), ephemeral: true);
    }

    IAsyncDownloader GetDownloaderFromManualProvider(ManualProviderOptions? provider)
    {
        return provider switch
        {
            ManualProviderOptions.YouTube => Downloader.YouTube,
            ManualProviderOptions.Spotify => Downloader.Spotify,
            ManualProviderOptions.SoundCloud => Downloader.SoundCloud,
            _ => Downloader.Default,
        };
    }

    Task<(IVoiceChannel?, IMediaRequest)> GetPlaybackContext(string query, ManualProviderOptions? provider)
    {
        var voice = (Context.User as SocketGuildUser)?.VoiceChannel;
        var downloader = provider is null ? Downloader.GetFromQuery(query) : GetDownloaderFromManualProvider(provider);
        DownloadRequest request = new(query.AsMemory(), downloader);
        return Task.FromResult(((IVoiceChannel?)voice, (IMediaRequest)request));
    }

    [SlashCommand("switch", "Switches the current song to the one specified.")]
    public async Task Switch(string query, ManualProviderOptions? provider)
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

        (var _, var request) = await GetPlaybackContext(query, provider);
        try
        {
            //TODO: Switch doesn't seem to work correctly when playing a playlist
            await jukebox.SwitchAsync(request);
            await DeleteOriginalResponseAsync();
        }
        catch (EmptyChannelException) { await ModifyOriginalResponseAsync(x => x.Content = "All users have left the channel. Disconnecting..."); }
    }

    [SlashCommand("play", "Plays or queues a song.")]
    public async Task Play(string query, ManualProviderOptions? provider = null)
    {
        if (query is null)
        {
            await RespondAsync("You need to specify a url or a search query!", ephemeral: true);
            return;
        }
        (var voice, var request) = await GetPlaybackContext(query, provider);
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

        try
        {
            Player player = new(Context.Interaction);
            await jukebox.PlayAsync(request, voice, player);
        }
        catch (EmptyChannelException)
        {
            await ModifyOriginalResponseAsync(x => x.Content = "All users have left the channel. Disconnecting...");
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
