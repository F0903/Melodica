﻿using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Melodica.Services.Caching;
using Melodica.Services.Downloaders;
using Melodica.Services.Playback.Exceptions;
using Melodica.Services.Playback.Requests;
using Melodica.Utility;

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
    private Jukebox Jukebox => cachedJukebox ??= JukeboxManager.GetOrCreateJukebox(Context.Guild);

    [SlashCommand("clear-cache", "Clears the media cache."), RequireOwner]
    public async Task ClearCache()
    {
        try
        {
            (var deletedFiles, var filesInUse, var ms) = await MediaFileCache.ClearAllCachesAsync();
            await RespondAsync($"Deleted {deletedFiles} files. ({filesInUse} files in use) [{ms}ms]", ephemeral: true);
        }
        catch (Exception e)
        {
            await RespondAsync($"Error occurred while clearing cache: {e.Message}", ephemeral: true);
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
        var info = Jukebox.CurrentSong;
        if (info is null)
        {
            await RespondAsync("Could not get song from jukebox. Contact developer.", ephemeral: true);
            return;
        }
        var songDur = info.Duration;
        await RespondAsync((songDur != TimeSpan.Zero ? $"__{songDur}__\n" : "") + $"{dur}", ephemeral: true);
    }

    [SlashCommand("clear", "Clears queue.")]
    public async Task Clear()
    {
        await Jukebox.Queue.ClearAsync();
        await RespondAsync("Cleared queue.", ephemeral: true);
    }

    [SlashCommand("remove", "Removes song from queue by index, or removes the last element if no parameter is given.")]
    public async Task Remove(int index = -1)
    {
        var queue = Jukebox.Queue;
        if (queue.IsEmpty)
        {
            await RespondAsync("Queue is empty.", ephemeral: true);
            return;
        }
        try
        {
            var removed = index == -1 ? await queue.RemoveAtAsync(^0) : await queue.RemoveAtAsync(index - 1);
            var removedInfo = await removed.GetInfoAsync();
            await RespondAsync(embed: new EmbedBuilder()
            {
                Title = "**Removed**",
                Description = removedInfo.Title
            }.Build(),
                ephemeral: true
            );
        }
        catch (IndexOutOfRangeException)
        {
            await RespondAsync($"The index was out of range. Accepted range: [1-{queue.Length}]", ephemeral: true);
        }

    }

    [SlashCommand("queue", "Shows the current queue.")]
    public async Task Queue()
    {
        await DeferAsync(ephemeral: true);
        var queue = Jukebox.Queue;
        EmbedBuilder eb = new();
        if (queue.IsEmpty)
        {
            eb.WithTitle("**Queue**")
              .WithDescription("No songs are queued.")
              .WithFooter("It's quite empty down here...");
        }
        else
        {
            var (queueDuration, imageUrl) = await queue.GetQueueInfo();
            eb.WithTitle("**Queue**")
              .WithThumbnailUrl(imageUrl)
              .WithFooter($"{(queueDuration == TimeSpan.Zero ? '\u221E'.ToString() : queueDuration.ToString())}{(Jukebox.Shuffle ? " | Shuffle" : "")}");

            var maxElems = 20;
            for (var i = 1; i <= maxElems; i++)
            {
                if (i > queue.Length)
                    break;
                var song = queue.GetAt(i - 1);
                var songInfo = await song.GetInfoAsync();
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
        if (!Jukebox.Playing)
        {
            await RespondAsync("No song is playing! Did you mean to use /play ?", ephemeral: true);
            return;
        }

        await DeferAsync(ephemeral: true);

        var downloader = Downloader.GetFromQuery(query);
        DownloaderRequest request = new(query.AsMemory(), downloader);

        // Get info to see if the request is actually valid.
        var info = await request.GetInfoAsync();

        await Jukebox.SetShuffleAsync(false);
        await Jukebox.SetNextAsync(request);
        await ModifyOriginalResponseAsync(x => x.Embed = EmbedUtils.CreateMediaEmbed(info, null));
    }

    static IAsyncDownloader GetDownloaderFromManualProvider(ManualProviderOptions? provider)
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
        var user = (Context.User as IGuildUser) ?? throw new NullReferenceException("Guild user cast failed. Could not get voice channel.");
        var voice = user.VoiceChannel;
        var downloader = provider is null ? Downloader.GetFromQuery(query) : GetDownloaderFromManualProvider(provider);
        DownloaderRequest request = new(query.AsMemory(), downloader);
        return ((IVoiceChannel?)voice, (IMediaRequest)request).WrapTask();
    }

    [SlashCommand("switch", "Switches the current song to the one specified.")]
    public async Task Switch(string query, ManualProviderOptions? provider = null)
    {
        if (!Jukebox.Playing)
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
            await Jukebox.SwitchAsync(request);
            await DeleteOriginalResponseAsync();
        }
        catch (EmptyChannelException) { await ModifyOriginalResponseAsync(x => x.Content = "All users have left the channel. Disconnecting..."); }
    }

    [SlashCommand("play", "Plays or queues a song."), RequireBotVoiceChannelPermission(ChannelPermission.Connect | ChannelPermission.Speak)]
    public async Task Play(string query, ManualProviderOptions? provider = null)
    {
        if (query is null)
        {
            await RespondAsync("You need to specify a url or a search query!", ephemeral: true);
            return;
        }
        var (voice, request) = await GetPlaybackContext(query, provider);
        if (voice is null)
        {
            await RespondAsync("You need to be in a voice channel!", ephemeral: true);
            return;
        }


        if (Jukebox.Playing) await DeferAsync(true);
        else await DeferAsync();

        try
        {
            JukeboxInterface player = new(Context.Interaction);
            var result = await Jukebox.PlayAsync(request, voice, player);
            if (result == Jukebox.PlayResult.Queued)
            {
                var info = await request.GetInfoAsync();
                var embed = EmbedUtils.CreateMediaEmbed(info, null);
                await ModifyOriginalResponseAsync(x =>
                {
                    x.Content = "The following media was queued:";
                    x.Embed = embed;
                });
            }
        }
        catch (EmptyChannelException)
        {
            await ModifyOriginalResponseAsync(x => x.Content = "All users have left the channel. Disconnecting...");
        }
    }

    [SlashCommand("abort", "Force the player to stop if the buttons aren't working.")]
    public async Task Abort()
    {
        await Jukebox.StopAsync();
        await RespondAsync("Stopped", ephemeral: true);
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
        await Jukebox.StopAsync();
    }

    [ComponentInteraction("player_shuffle")]
    public async Task Shuffle()
    {
        await DeferAsync();
        await Jukebox.SetShuffleAsync(!Jukebox.Shuffle);
    }

    [ComponentInteraction("player_repeat")]
    public async Task Repeat()
    {
        await DeferAsync();
        await Jukebox.SetRepeatAsync(!Jukebox.Repeat);
    }

    [ComponentInteraction("player_loop")]
    public async Task Loop()
    {
        await DeferAsync();
        await Jukebox.SetLoopAsync(!Jukebox.Loop);
    }

    [ComponentInteraction("player_togglepause")]
    public async Task TogglePause()
    {
        await DeferAsync();
        await Jukebox.SetPausedAsync(!Jukebox.Paused);
    }

    [ComponentInteraction("player_skip")]
    public async Task Skip()
    {
        await DeferAsync();
        await Jukebox.SkipAsync();
    }
}
