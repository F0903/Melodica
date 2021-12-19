
using Discord;
using Discord.Commands;
using Discord.WebSocket;

using Melodica.Services.Caching;
using Melodica.Services.Downloaders;
using Melodica.Services.Media;
using Melodica.Services.Playback.Exceptions;
using Melodica.Services.Playback.Requests;
using Melodica.Utility.Extensions;

namespace Melodica.Services.Playback;

public class JukeboxCommands : ModuleBase<SocketCommandContext>
{
    Jukebox? cachedJukebox;
    private Jukebox Jukebox => cachedJukebox ??=
        JukeboxManager.GetOrCreateJukebox(Context.Guild, () => new Jukebox(Context.Channel));

    private IVoiceChannel GetUserVoiceChannel()
    {
        return ((SocketGuildUser)Context.User).VoiceChannel;
    }

    private Task<IMediaRequest> GetRequestAsync(string query)
    {
        IReadOnlyCollection<Attachment>? attach = Context.Message.Attachments;
        if (attach.Count != 0)
        {
            return Task.FromResult(new AttachmentMediaRequest(attach.ToArray()) as IMediaRequest);

        }
        IAsyncDownloader? downloader = DownloaderResolver.GetDownloaderFromQuery(query) ?? (query.IsUrl() ? null : IAsyncDownloader.Default);
        IMediaRequest request = downloader == null ? new URLMediaRequest(query) : new DownloadRequest(query!.AsMemory(), downloader);
        return Task.FromResult(request);
    }

    [Command("ClearCache"), Summary("Clears cache."), RequireOwner]
    public async Task ClearCacheAsync()
    {
        (int deletedFiles, int filesInUse, long ms) = await MediaFileCache.ClearAllCachesAsync();
        await ReplyAsync($"Deleted {deletedFiles} files. ({filesInUse} files in use) [{ms}ms]");
    }

    [Command("Shuffle"), Summary("Toggles shuffle.")]
    public async Task ShuffleAsync()
    {
        bool state = Jukebox.Shuffle = !Jukebox.Shuffle;
        await ReplyAsync($"Shuffle {(state ? "On" : "Off")}");
    }

    [Command("Repeat"), Alias("Keep"), Summary("Toggles repeat of the queue.")]
    public async Task ToggleRepeatAsync()
    {
        bool state = Jukebox.Repeat = !Jukebox.Repeat;
        await ReplyAsync($"Repeat {(state ? "On" : "Off")}");
    }

    [Command("Loop"), Summary("Toggles loop on the current song.")]
    public async Task SetLoopingAsync()
    {
        if (!Jukebox.Playing)
        {
            await ReplyAsync("Cannot set loop when no song is playing.");
            return;
        }

        bool state = Jukebox.Loop = !Jukebox.Loop;
        await ReplyAsync($"Loop {(state ? "On" : "Off")}");
    }

    [Command("Song"), Alias("Info", "SongInfo"), Summary("Gets info about the current song.")]
    public async Task GetSongAsync()
    {
        if (!Jukebox.Playing)
        {
            await ReplyAsync("No song is playing.");
            return;
        }
        PlayableMedia? media = Jukebox.GetSong();
        if (media is null)
        {
            await ReplyAsync("GetSong returned null.");
            return;
        }
        Embed? embed = EmbedUtils.CreateMediaEmbed(media.Info, media.CollectionInfo, MediaState.Queued);
        await ReplyAsync(null, false, embed);
    }

    [Command("Duration"), Summary("Gets the elapsed time of the song.")]
    public async Task GetDurationAsync()
    {
        if (!Jukebox.Playing)
        {
            await ReplyAsync("No song is playing.");
            return;
        }

        TimeSpan dur = Jukebox.Elapsed;
        PlayableMedia? song = Jukebox.GetSong();
        if (song is null)
        {
            await ReplyAsync("Could not get song from jukebox.");
            return;
        }
        TimeSpan songDur = song.Info.Duration;
        await ReplyAsync((songDur != TimeSpan.Zero ? $"__{songDur}__\n" : "") + $"{dur}");
    }

    [Command("Resume"), Summary("Resumes playback.")]
    public Task ResumeAsync()
    {
        Jukebox.Paused = false;
        return Task.CompletedTask;
    }

    [Command("Pause"), Summary("Pauses playback.")]
    public Task PauseAsync()
    {
        Jukebox.Paused = true;
        return Task.CompletedTask;
    }

    [Command("Skip"), Summary("Skips current song.")]
    public async Task SkipAsync()
    {
        await Jukebox.SkipAsync();
    }

    [Command("Clear"), Summary("Clears queue.")]
    public async Task ClearQueue()
    {
        await Jukebox.ClearAsync();
        await ReplyAsync("Cleared queue.");
    }

    [Command("Remove"), Summary("Removes song from queue by index, or removes the last element if no parameter is given.")]
    public async Task RemoveSongFromQueue(int? index = null)
    {
        MediaQueue? queue = Jukebox.GetQueue();
        // If index is null (default) then remove the last element.
        PlayableMedia removed = index == null ? await queue.RemoveAtAsync(^0) : await queue.RemoveAtAsync(index.Value - 1);
        MediaInfo? removedInfo = removed.Info;
        await ReplyAsync(null, false, new EmbedBuilder()
        {
            Title = "**Removed**",
            Description = removedInfo.Title
        }.Build());
    }

    [Command("Queue"), Summary("Shows current queue.")]
    public async Task QueueAsync()
    {
        MediaQueue? queue = Jukebox.GetQueue();
        EmbedBuilder? eb = new();
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
                PlayableMedia? song = queue[i - 1];
                MediaInfo? songInfo = song.Info;
                eb.AddField(
                    i == 1 ? "Next:" : i == maxElems ? "And more" : i.ToString(),
                    i == 1 ? $"**{songInfo.Artist} - {songInfo.Title}**" : i == maxElems ? $"Plus {queue.Length - (i - 1)} other songs!" : $"{songInfo.Artist} - {songInfo.Title}",
                    false);
            }
        }

        await Context.Channel.SendMessageAsync(null, false, eb.Build());
    }

    [Command("Next"), Summary("Sets the next song to play.")]
    public async Task NextAsync([Remainder] string query)
    {
        if (!Jukebox.Playing)
        {
            await PlayAsync(query);
            return;
        }

        IMediaRequest? request = await GetRequestAsync(query);

        // Get info to see if the request is actually valid.
        MediaInfo? info = await request.GetInfoAsync();

        Jukebox.Shuffle = false;
        await Jukebox.SetNextAsync(request);
        await ReplyAsync(null, false, EmbedUtils.CreateMediaEmbed(info, null, MediaState.Queued));
    }

    [Command("Switch"), Summary("Changes the current song.")]
    public async Task SwitchAsync([Remainder] string? mediaQuery = null)
    {
        IVoiceChannel? userVoice = GetUserVoiceChannel();
        if (userVoice == null)
        {
            await ReplyAsync("You need to be in a voice channel!");
            return;
        }

        GuildPermissionsChecker.AssertVoicePermissions(Context.Guild, Context.Client.CurrentUser, userVoice);

        if (mediaQuery == null && Context.Message.Attachments.Count == 0)
        {
            await ReplyAsync("You need to specify a url, search query or upload a file.");
            return;
        }

        try
        {
            //TODO: Switch doesn't seem to work correctly when playing a playlist
            Jukebox? jukebox = Jukebox;
            if (jukebox.Playing)
            {
                await jukebox.SwitchAsync(await GetRequestAsync(mediaQuery!));
            }
            else
            {
                await jukebox.PlayAsync(await GetRequestAsync(mediaQuery!), userVoice);
            }
        }
        catch (EmptyChannelException) { await ReplyAsync("All users have left the channel. Disconnecting..."); }
    }

    [Command("Play"), Summary("Plays the specified song.")]
    public async Task PlayAsync([Remainder] string? mediaQuery = null)
    {
        IVoiceChannel? userVoice = GetUserVoiceChannel();
        if (userVoice == null)
        {
            await ReplyAsync("You need to be in a voice channel!");
            return;
        }

        GuildPermissionsChecker.AssertVoicePermissions(Context.Guild, Context.Client.CurrentUser, userVoice);

        if (mediaQuery == null && Context.Message.Attachments.Count == 0)
        {
            await ReplyAsync("You need to specify a url, search query or upload a file.");
            return;
        }

        try { await Jukebox.PlayAsync(await GetRequestAsync(mediaQuery!), userVoice); }
        catch (EmptyChannelException) { await ReplyAsync("All users have left the channel. Disconnecting..."); }
    }

    [Command("Stop"), Summary("Stops playback.")]
    public async Task StopAsync()
    {
        if (!Jukebox.Playing)
        {
            await ReplyAsync("No song is playing.");
            return;
        }
        await Jukebox.StopAsync();
    }
}
