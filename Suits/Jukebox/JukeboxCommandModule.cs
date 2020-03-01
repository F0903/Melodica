using Suits.Jukebox;
using Suits.Jukebox.Models.Requests;
using Suits.Jukebox.Services.Cache;
using Suits.Jukebox.Services.Downloaders;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Suits.Utility.Extensions;
using Suits.Jukebox.Models;

namespace Suits.Jukebox
{
    //[Group("Jukebox"), Alias("J")]
    public class JukeboxCommandModule : ModuleBase<SocketCommandContext>
    {
        public JukeboxCommandModule(JukeboxService jukebox)
        {
            this.jukebox = jukebox;
        }

        private readonly JukeboxService jukebox;

        private IVoiceChannel GetUserVoiceChannel() => ((SocketGuildUser)Context.User).VoiceChannel;

        private Embed GetMediaEmbed(string title, IMediaInfo media, string? description = null)
        {
            return new EmbedBuilder().WithTitle(title)
                                     .WithDescription(description ?? media.GetTitle())
                                     .WithFooter(media.GetDuration().ToString())
                                     .WithThumbnailUrl(media.GetThumbnail()).Build();
        }

        private async void LargeMediaCallback() => await ReplyAsync("Large media detected. This might take a while.");

        private async void MediaUnavailableCallback(string vid) => await ReplyAsync($"{vid} was unavailable. Skipping...");

        [Command("ClearCache"), Summary("Clears cache."), RequireOwner]
        public async Task ClearCacheAsync()
        {
            var (deletedFiles, filesInUse, ms) = await (await jukebox.GetJukeboxAsync(Context.Guild)).GetCache().PruneCacheAsync(true);
            await (await jukebox.GetJukeboxAsync(Context.Guild)).ClearQueueAsync();
            await ReplyAsync($"Deleted {deletedFiles} files. ({filesInUse} files in use) [{ms}ms]");
        }

        [Command("Shuffle"), Summary("Toggles shuffle.")]
        public async Task ShuffleAsync()
        {
            var juke = await jukebox.GetJukeboxAsync(Context.Guild);
            await juke.ToggleShuffleAsync(async (x, b) => await ReplyAsync(null, false, GetMediaEmbed(b ? "**Shuffling**" : "**Stopped Shuffling**", x)));
        }

        [Command("Loop"), Summary("Toggles loop on the current song.")]
        public async Task SetLoopingAsync([Remainder] string? query = null)
        {
            var juke = await jukebox.GetJukeboxAsync(Context.Guild);
            if (!juke.Playing)
            {
                if (query == null)
                    return;
                //await juke.PlayAsync(new DownloadMediaRequest(query!, juke.GetCache(), Context.Guild, QueueMode.Fast, LargeMediaCallback), GetUserVoiceChannel(), false);
            }
            await juke.ToggleLoopAsync(async (x, l) => await ReplyAsync(null, false, GetMediaEmbed(l ? "**Now Looping**" : "**Stopped Looping**", x)));
        }

        [Command("Song"), Alias("Info", "SongInfo"), Summary("Gets info about the current song.")]
        public async Task GetSongAsync()
        {
            var song = (await jukebox.GetJukeboxAsync(Context.Guild)).CurrentSong;
            if (song == null)
            {
                await ReplyAsync("No song is playing.");
                return;
            }
            await ReplyAsync(null, false, GetMediaEmbed("**Currently Playing**", song));
        }

        [Command("Resume"), Summary("Resumes playback.")]
        public async Task ResumeAsync()
        {
            (await jukebox.GetJukeboxAsync(Context.Guild)).Paused = false;
        }

        [Command("Pause"), Alias("Unpause"), Summary("Pauses playback.")]
        public async Task PauseAsync()
        {
            (await jukebox.GetJukeboxAsync(Context.Guild)).Paused = true;
        }

        [Command("Skip"), Summary("Skips current song.")]
        public async Task SkipAsync()
        {
            (await jukebox.GetJukeboxAsync(Context.Guild)).Skip();
        }

        [Command("Clear"), Summary("Clears queue.")]
        public async Task ClearQueue()
        {
            await (await jukebox.GetJukeboxAsync(Context.Guild)).ClearQueueAsync();
            await ReplyAsync("Cleared queue.");
        }

        [Command("Remove"), Summary("Removes song from queue by index.")]
        public async Task RemoveSongFromQueue(int index)
        {
            var removed = await (await jukebox.GetJukeboxAsync(Context.Guild)).RemoveFromQueueAsync(index - 1);
            await ReplyAsync($"Removed {removed.Meta.Title} from queue.");
        }

        [Command("Queue"), Summary("Shows current queue.")]
        public async Task QueueAsync()
        {
            var juke = await jukebox.GetJukeboxAsync(Context.Guild);

            var queue = juke.GetQueue();
            if (queue.IsEmpty)
            {
                await ReplyAsync("No songs are queued.");
                return;
            }

            EmbedBuilder eb = new EmbedBuilder()
            .WithTitle("**Queue**")
            .WithThumbnailUrl(queue.ToMediaCollection().GetThumbnail())
            .WithFooter($"Duration - {juke.GetQueue().GetTotalDuration()} | Shuffle - {(juke.Shuffle ? "On" : "Off")}");

            int maxElems = 20;
            for (int i = 1; i <= maxElems; i++)
            {
                if (i > queue.Length)
                    break;
                var x = queue[i - 1];
                eb.AddField(i == 1 ? "Next:" : i == maxElems ? "And more" : i.ToString(), i == 1 ? $"**{x.Meta.Title}**" : i == maxElems ? $"Plus {queue.Length - (i - 1)} other songs!" : x.Meta.Title, false);
            }
            await Context.Channel.SendMessageAsync(null, false, eb.Build());
        }

        [Command("Switch"), Alias("Change"), Summary("Changes the current song.")]
        public async Task SwitchAsync([Remainder] string songQuery)
        {
            if (GetUserVoiceChannel() == null)
            {
                await ReplyAsync("You need to be in a voice channel!");
                return;
            }

            var juke = await jukebox.GetJukeboxAsync(Context.Guild);

            // Refactor this request class.
            var request = new DownloadMediaRequest(songQuery, juke.GetCache(), Context.Guild, juke.Playing ? QueueMode.Consistent : QueueMode.Fast, LargeMediaCallback, MediaUnavailableCallback);

            await juke.PlayAsync(request, GetUserVoiceChannel(), true, async (context) =>
            {
                await ReplyAsync(null, false, GetMediaEmbed("**Now Playing**", context.media));
            });
        }

        [Command("Play"), Alias("P"), Summary("Plays the specified song.")]
        public async Task PlayAsync([Remainder] string? songQuery = null)
        {
            if (GetUserVoiceChannel() == null)
            {
                await ReplyAsync("You need to be in a voice channel!");
                return;
            }

            var attach = Context.Message.Attachments;

            if (songQuery == null && attach == null)
            {
                await ReplyAsync("You need to specify a url, search query or upload a file.");
                return;
            }

            var juke = await jukebox.GetJukeboxAsync(Context.Guild);

            MediaRequest request;
            if (attach.Count == 0)
            {
                // Refactor this request class.
                request = new DownloadMediaRequest(songQuery!, (await jukebox.GetJukeboxAsync(Context.Guild)).GetCache(), Context.Guild,
                        (await jukebox.GetJukeboxAsync(Context.Guild)).Playing ? QueueMode.Consistent : QueueMode.Fast, LargeMediaCallback, MediaUnavailableCallback);
            }
            else
            {
                request = new AttachmentMediaRequest(attach.ToArray(), (await jukebox.GetJukeboxAsync(Context.Guild)).GetCache());
            }

            await juke.PlayAsync(request, GetUserVoiceChannel(), false, async (context) =>
            {
                await ReplyAsync(null, false, GetMediaEmbed(context.queued ? "**Queued**" : "**Now Playing**", context.media));
            });
        }

        [Command("Stop"), Summary("Stops playback.")]
        public async Task StopAsync()
        {
            if (!(await jukebox.GetJukeboxAsync(Context.Guild)).Playing)
            {
                await ReplyAsync("No song is playing.");
                return;
            }
            await (await jukebox.GetJukeboxAsync(Context.Guild)).StopAsync();

            await ReplyAsync("Stopped playback.");
        }
    }
}