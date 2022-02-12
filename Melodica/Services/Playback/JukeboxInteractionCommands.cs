using System.Linq;

using Discord;
using Discord.Interactions;
using Discord.WebSocket;

using Melodica.Services.Caching;
using Melodica.Services.Media;
using Melodica.Services.Playback.Exceptions;
using Melodica.Services.Playback.Requests;

namespace Melodica.Services.Playback;

public class JukeboxInteractionCommands : InteractionModuleBase<SocketInteractionContext>
{
    Jukebox? cachedJukebox;
    private Jukebox Jukebox => cachedJukebox ??=
        JukeboxManager.GetOrCreateJukebox(Context.Guild, () => new Jukebox(Context.Channel));

    [SlashCommand("clear-cache", "Clears the media cache.")]
    public async Task ClearCache()
    {
        (int deletedFiles, int filesInUse, long ms) = await MediaFileCache.ClearAllCachesAsync();
        await RespondAsync($"Deleted {deletedFiles} files. ({filesInUse} files in use) [{ms}ms]");
    }

    [SlashCommand("duration", "Shows the remaining duration of the current song.")]
    public async Task Duration()
    {
        if (!Jukebox.Playing)
        {
            await RespondAsync("No song is playing :(");
            return;
        }

        TimeSpan dur = Jukebox.Elapsed;
        PlayableMedia? song = Jukebox.GetSong();
        if (song is null)
        {
            await RespondAsync("Could not get song from jukebox. Contact developer.");
            return;
        }
        TimeSpan songDur = song.Info.Duration;
        await RespondAsync((songDur != TimeSpan.Zero ? $"__{songDur}__\n" : "") + $"{dur}");
    }

    [SlashCommand("clear", "Clears queue.")]
    public async Task Clear()
    {
        await Jukebox.ClearAsync();
        await RespondAsync("Cleared queue.");
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
        }.Build());
    }

    [SlashCommand("queue", "Shows the current queue.")]
    public async Task Queue()
    {
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

    [SlashCommand("next", "Sets the next song to play.")]
    public async Task Next(string query)
    {
        var request = new DownloadRequest(query.AsMemory());

        // Get info to see if the request is actually valid.
        MediaInfo? info = await request.GetInfoAsync();

        Jukebox.Shuffle = false;
        await Jukebox.SetNextAsync(request);
        await RespondAsync(embed: EmbedUtils.CreateMediaEmbed(info, null, MediaState.Queued));
    }

    Task<(IVoiceChannel?, IMediaRequest)> GetPlaybackContext(string query)
    {
        var voice = (Context.User as SocketGuildUser)?.VoiceChannel;
        var request = new DownloadRequest(query.AsMemory());
        return Task.FromResult(((IVoiceChannel?)voice, (IMediaRequest)request));
    }

    async Task SetButtonState(string buttonId, bool state)
    {
        var playerInt = (SocketMessageComponent)Context.Interaction;
        var msg = playerInt.Message;
        var msgComps = msg.Components;
        await msg.ModifyAsync(x =>
        {
            var compBuilder = new ComponentBuilder();
            foreach (var row in msgComps)
            {
                var rowBuilder = new ActionRowBuilder();
                foreach (var comp in row.Components)
                {
                    var toAdd = comp;
                    if (comp.CustomId == buttonId)
                    {
                        toAdd = (comp as ButtonComponent)!
                            .ToBuilder()
                            .WithStyle(state ? ButtonStyle.Primary : ButtonStyle.Secondary)
                            .Build();
                    }
                    rowBuilder.AddComponent(toAdd);
                }
                compBuilder.AddRow(rowBuilder);
            }
            x.Components = compBuilder.Build();
        });
    }

    async Task DisableAllButtons()
    {
        var playerInt = (SocketMessageComponent)Context.Interaction;
        var msg = playerInt.Message;
        var msgComps = msg.Components;
        await msg.ModifyAsync(x =>
        {
            var compBuilder = new ComponentBuilder();
            foreach (var row in msgComps)
            {
                var rowBuilder = new ActionRowBuilder();
                foreach (var comp in row.Components)
                {
                    var toAdd = (comp as ButtonComponent)!
                        .ToBuilder()
                        .WithStyle(ButtonStyle.Secondary)
                        .WithDisabled(true)
                        .Build();
                    rowBuilder.AddComponent(toAdd);
                }
                compBuilder.AddRow(rowBuilder);
            }
            x.Components = compBuilder.Build();
        });
    }

    [SlashCommand("switch", "Switches the current song to the one specified.")]
    public async Task Switch(string query)
    {
        var (voice, request) = await GetPlaybackContext(query);
        if (voice is null)
        {
            await RespondAsync("You need to be in a voice channel!");
            return;
        }

        if (!GuildPermissionsChecker.CheckVoicePermission(Context.Guild, Context.Client.CurrentUser, voice))
        {
            await RespondAsync("I don't have permission to connect and speak in this channel :(");
            return;
        }

        if (query is null)
        {
            await RespondAsync("You need to specify a url or search query.");
            return;
        }

        try
        {
            //TODO: Switch doesn't seem to work correctly when playing a playlist
            var jukebox = Jukebox;
            if (jukebox.Playing)
            {
                await jukebox.SwitchAsync(request);
            }
            else
            {
                await jukebox.PlayAsync(request, voice);
            }
        }
        catch (EmptyChannelException) { await RespondAsync("All users have left the channel. Disconnecting..."); }
    }

    [SlashCommand("play", "Starts playing a song.")]
    public async Task Play(string query)
    {
        await DeferAsync(); // Command can take a long time.

        var (voice, request) = await GetPlaybackContext(query);
        if (voice is null)
        {
            await ModifyOriginalResponseAsync(x => x.Content = "You need to be in a voice channel!");
            return;
        }

        if (!GuildPermissionsChecker.CheckVoicePermission(Context.Guild, Context.Client.CurrentUser, voice))
        {
            await ModifyOriginalResponseAsync(x => x.Content = "I don't have permission to connect and speak in this channel :(");
            return;
        }

        if (query is null)
        {
            await ModifyOriginalResponseAsync(x => x.Content = "You need to specify a url, search query or upload a file.");
            return;
        }

        var player =
            new ComponentBuilder()
            .WithButton(customId: "player_togglepause", emote: Emoji.Parse(":arrow_forward:"))
            .WithButton(customId: "player_stop", emote: Emoji.Parse(":stop_button:"), style: ButtonStyle.Secondary)
            .WithButton(customId: "player_skip", emote: Emoji.Parse(":track_next:"), style: ButtonStyle.Secondary)
            .WithButton(customId: "player_shuffle", emote: Emoji.Parse(":twisted_rightwards_arrows:"), style: ButtonStyle.Secondary)
            .WithButton(customId: "player_repeat", emote: Emoji.Parse(":repeat:"), style: ButtonStyle.Secondary)
            .WithButton(customId: "player_loop", emote: Emoji.Parse(":repeat_one:"), style: ButtonStyle.Secondary)
            .Build();

        var mediaInfo = await request.GetInfoAsync();
        var col = await request.GetMediaAsync();
        var colInfo = col.CollectionInfo;

        await ModifyOriginalResponseAsync(x =>
        {
            x.Embed = EmbedUtils.CreateMediaEmbed(mediaInfo, colInfo, MediaState.Queued);
            x.Components = player;
        });

        try { await Jukebox.PlayAsync(request, voice); }
        catch (EmptyChannelException) { await ModifyOriginalResponseAsync(x => x.Content = "All users have left the channel. Disconnecting..."); }
    }

    [SlashCommand("abort", "Stop the bot if the player doesn't work normally.")]
    public async Task Abort()
    {
        await Jukebox.StopAsync();
        await RespondAsync("Stopped...");
    }

    [ComponentInteraction("player_stop")]
    public async Task Stop()
    {
        if (!Jukebox.Playing)
        {
            await RespondAsync("No song is playing.");
            return;
        }

        await Jukebox.StopAsync();
        await DisableAllButtons();
        await DeferAsync();
    }

    [ComponentInteraction("player_shuffle")]
    public async Task Shuffle()
    {
        bool state = Jukebox.Shuffle = !Jukebox.Shuffle;
        await SetButtonState("player_shuffle", state);
        await DeferAsync();
    }

    [ComponentInteraction("player_repeat")]
    public async Task Repeat()
    {
        bool state = Jukebox.Repeat = !Jukebox.Repeat;
        await SetButtonState("player_repeat", state);
        await DeferAsync();
    }

    [ComponentInteraction("player_loop")]
    public async Task Loop()
    {
        bool state = Jukebox.Loop = !Jukebox.Loop;
        await SetButtonState("player_loop", state);
        await DeferAsync();
    }

    [ComponentInteraction("player_togglepause")]
    public async Task TogglePause()
    {
        bool state = Jukebox.Paused = !Jukebox.Paused;
        await SetButtonState("player_togglepause", !state);
        await DeferAsync();
    }

    [ComponentInteraction("player_skip")]
    public async Task Skip()
    {
        await Jukebox.SkipAsync();
        await DeferAsync();
    }
}
