﻿using Suits.Jukebox;
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

        private async void LargeMediaCallback() => await ReplyAsync("Large media detected. This might take a while.");

        private async void MediaUnavailableCallback(string vid) => await ReplyAsync($"{vid} was unavailable. Skipping...");

        [Command("ClearCache"), Summary("Clears cache."), RequireOwner]
        public async Task ClearCacheAsync()
        {
            var (deletedFiles, filesInUse, ms) = await (await jukebox.GetJukeboxAsync(Context.Guild)).GetCache().PruneCacheAsync(true);
            await ReplyAsync($"Deleted {deletedFiles} files. ({filesInUse} files in use) [{ms}ms]");
        }

        [Command("Shuffle"), Summary("Shuffles the queue.")]
        public async Task SetShuffleAsync(bool val = true)
        {
            (await jukebox.GetJukeboxAsync(Context.Guild)).Shuffle = val;
            await ReplyAsync($"Shuffle set to {val}");
        }

        [Command("IsLooping"), Summary("")]
        public async Task IsLoopingAsync()
        {
            await ReplyAsync($"Loop is set to {(await jukebox.GetJukeboxAsync(Context.Guild)).Looping}");
        }

        [Command("Loop"), Summary("Loops the current song.")]
        public async Task SetLoopingAsync(bool val = true)
        {
            var juke = await jukebox.GetJukeboxAsync(Context.Guild);
            juke.Looping = val;
            await ReplyAsync($"Loop set to {val}");
        }

        [Command("Song"), Summary("Gets the currently playing song.")]
        public async Task GetSongAsync()
        {
            var song = (await jukebox.GetJukeboxAsync(Context.Guild)).CurrentSong;
            await ReplyAsync(song != null ? $"**Currently playing** {song}" : "No song is playing.");
        }

        [Command("Duration"), Summary("Gets the duration of the playing song.")]
        public async Task GetDurationAsync()
        {
            await ReplyAsync($"Duration is {(await jukebox.GetJukeboxAsync(Context.Guild)).CurrentSong.Meta.Duration}");
        }

        [Command("Resume"), Summary("Resumes playback.")]
        public async Task ResumeAsync()
        {
            (await jukebox.GetJukeboxAsync(Context.Guild)).Paused = false;
        }

        [Command("Pause"), Summary("Pauses playback or sets the pause status if a parameter is specified.")]
        public async Task PauseAsync(bool? val = null)
        {
            (await jukebox.GetJukeboxAsync(Context.Guild)).Paused = val ?? true;
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
            
            EmbedBuilder eb = new EmbedBuilder
            {
                Color = Color.DarkGrey
            };

            eb.WithTitle("**Queue**");

            for (int i = 1; i <= 10; i++)
            {
                if (i > queue.Length)
                    break;
                var x = queue[i - 1];
                eb.AddField(i == 1 ? "Next:" : i == 10 ? "And more" : i.ToString(), i == 1 ? $"**{x.Meta.Title}**" : i == 10 ? $"Plus {queue.Length - (i - 1)} other songs!" : x.Meta.Title, false);
            }

            eb.WithFooter($"Duration - {juke.GetQueue().GetTotalDuration()} | Shuffle - {(juke.Shuffle ? "On" : "Off")}");

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

            // Rewrite this request class.
            var request = new DownloadMediaRequest<AsyncYoutubeDownloader>(songQuery, juke.GetCache(), Context.Guild, juke.Playing ? QueueMode.Consistent : QueueMode.Fast, LargeMediaCallback, MediaUnavailableCallback);

            await juke.PlayAsync(request, GetUserVoiceChannel(), true, async (context) =>
            {
                var embed = new EmbedBuilder().WithTitle("**Now Playing**")
                                              .WithDescription(context.media.GetTitle())
                                              .WithFooter(context.media.GetDuration().ToString());
                await ReplyAsync(null, false, embed.Build());
            });
        }

        [Command("Play"), Alias("P"), Summary("Plays the specified song.")]
        public async Task PlayAsync([Remainder] string songQuery = null)
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
            if(attach.Count == 0)
            {
                // Rewrite this request class.
                request = new DownloadMediaRequest<AsyncYoutubeDownloader>(songQuery, (await jukebox.GetJukeboxAsync(Context.Guild)).GetCache(), Context.Guild,
                        (await jukebox.GetJukeboxAsync(Context.Guild)).Playing ? QueueMode.Consistent : QueueMode.Fast, LargeMediaCallback, MediaUnavailableCallback);
            }
            else
            {
                request = new AttachmentMediaRequest(attach.ToArray(), (await jukebox.GetJukeboxAsync(Context.Guild)).GetCache());
            }

            await juke.PlayAsync(request, GetUserVoiceChannel(), false, async (context) =>
            {
                var embed = new EmbedBuilder().WithTitle((context.queued ? "**Queued**" : "**Now Playing**"))
                                              .WithDescription(context.media.GetTitle())
                                              .WithFooter(context.media.GetDuration().ToString());
                await ReplyAsync(null, false, embed.Build());
            });
        }

        [Command("Stop"), Summary("Stops playback.")]
        public async Task StopAsync()
        {
            if(!(await jukebox.GetJukeboxAsync(Context.Guild)).Playing)
            {
                await ReplyAsync("No song is playing.");
                return;
            }
            await (await jukebox.GetJukeboxAsync(Context.Guild)).StopAsync();

            await ReplyAsync("Stopped playback.");
        }
    }
}