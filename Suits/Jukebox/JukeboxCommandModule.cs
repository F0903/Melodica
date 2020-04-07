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
using System.Collections.Generic;

namespace Suits.Jukebox
{
    //[Group("Jukebox"), Alias("J")]
    public class JukeboxCommandModule : ModuleBase<SocketCommandContext>
    {
        public JukeboxCommandModule(AsyncYoutubeDownloader dl)
        {
            this.downloader = dl;

            downloader.LargeSizeWarningCallback = LargeMediaCallback;
            downloader.VideoUnavailableCallback = MediaUnavailableCallback;
        }

        private readonly AsyncYoutubeDownloader downloader; // Temp until a better solution is figured out for more platforms

        private IVoiceChannel GetUserVoiceChannel() => ((SocketGuildUser)Context.User).VoiceChannel;

        private Embed GetMediaEmbed(string embedTitle, IMediaInfo media, Color? color = null, string? description = null)
        {
            return new EmbedBuilder().WithColor(color ?? Color.DarkGrey)
                                     .WithTitle(embedTitle)
                                     .WithDescription(description ?? media.GetTitle())
                                     .WithFooter(media.GetDuration().ToString())
                                     .WithThumbnailUrl(media.GetThumbnail()).Build();
        }

        private async void LargeMediaCallback() => await ReplyAsync("Large media detected. This might take a while.");

        private async void MediaUnavailableCallback(string vid) => await ReplyAsync($"{vid} was unavailable. Skipping...");

        private Task<MediaRequest> GetRequestAsync(string query)
        {
            var attach = Context.Message.Attachments;
            if (attach.Count != 0)
            {
                return Task.FromResult(new AttachmentMediaRequest(attach.ToArray()) as MediaRequest);
            }
            else
            {
                return Task.FromResult(new DownloadRequest<AsyncYoutubeDownloader>(query!) as MediaRequest);
            }
        }

        [Command("ClearCache"), Summary("Clears cache."), RequireOwner]
        public async Task ClearCacheAsync()
        {
            var (deletedFiles, filesInUse, ms) = await MediaCache.PruneCacheAsync(true);
            await (await JukeboxService.GetJukeboxAsync(Context.Guild)).ClearQueueAsync();
            await ReplyAsync($"Deleted {deletedFiles} files. ({filesInUse} files in use) [{ms}ms]");
        }

        [Command("Shuffle"), Summary("Toggles shuffle.")]
        public async Task ShuffleAsync()
        {
            var juke = await JukeboxService.GetJukeboxAsync(Context.Guild);
            await juke.ToggleShuffleAsync(async (x, b) => await ReplyAsync(null, false, GetMediaEmbed(b ? "**Shuffling**" : "**Stopped Shuffling**", x)));
        }

        [Command("Loop"), Summary("Toggles loop on the current song.")]
        public async Task SetLoopingAsync([Remainder] string? query = null)
        {
            var juke = await JukeboxService.GetJukeboxAsync(Context.Guild);
            if (!juke.Playing)
            {
                if (query == null)
                    return;
                //TODO: Complete this
            }
            await juke.ToggleLoopAsync(async (x, l) => await ReplyAsync(null, false, GetMediaEmbed(l ? "**Now Looping**" : "**Stopped Looping**", x)));
        }

        [Command("Song"), Alias("Info", "SongInfo"), Summary("Gets info about the current song.")]
        public async Task GetSongAsync()
        {
            var song = (await JukeboxService.GetJukeboxAsync(Context.Guild)).CurrentSong;
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
            (await JukeboxService.GetJukeboxAsync(Context.Guild)).Paused = false;
        }

        [Command("Pause"), Alias("Unpause"), Summary("Pauses playback.")]
        public async Task PauseAsync()
        {
            (await JukeboxService.GetJukeboxAsync(Context.Guild)).Paused = true;
        }

        [Command("Skip"), Summary("Skips current song.")]
        public async Task SkipAsync()
        {
            (await JukeboxService.GetJukeboxAsync(Context.Guild)).Skip();
        }

        [Command("Clear"), Summary("Clears queue.")]
        public async Task ClearQueue()
        {
            await (await JukeboxService.GetJukeboxAsync(Context.Guild)).ClearQueueAsync();
            await ReplyAsync("Cleared queue.");
        }

        [Command("Remove"), Summary("Removes song from queue by index.")]
        public async Task RemoveSongFromQueue(int index)
        {
            var removed = (await JukeboxService.GetJukeboxAsync(Context.Guild)).RemoveFromQueue(index - 1);
            await ReplyAsync(null, false, GetMediaEmbed("Removed", removed));
        }

        [Command("Queue"), Summary("Shows current queue.")]
        public async Task QueueAsync()
        {
            var juke = await JukeboxService.GetJukeboxAsync(Context.Guild);

            var queue = juke.GetQueue();
            if (queue.IsEmpty)
            {
                await ReplyAsync("No songs are queued.");
                return;
            }

            EmbedBuilder eb = new EmbedBuilder()
            .WithTitle("**Queue**")
            .WithThumbnailUrl(queue.GetMediaInfo().GetThumbnail());
            //.WithFooter($"Duration - {queue.GetTotalDuration()} | Shuffle - {(juke.Shuffle ? "On" : "Off")}");

            int maxElems = 20;
            for (int i = 1; i <= maxElems; i++)
            {
                if (i > queue.Length)
                    break;
                var x = queue[i - 1];
                eb.AddField(i == 1 ? "Next:" : i == maxElems ? "And more" : i.ToString(), i == 1 ? $"**{x.GetMediaInfo().GetTitle()}**" : i == maxElems ? $"Plus {queue.Length - (i - 1)} other songs!" : x.GetMediaInfo().GetTitle(), false);
            }
            await Context.Channel.SendMessageAsync(null, false, eb.Build());
        }

        [Command("Switch"), Alias("Change"), Summary("Changes the current song.")]
        public async Task SwitchAsync([Remainder] string mediaQuery)
        {
            if (GetUserVoiceChannel() == null)
            {
                await ReplyAsync("You need to be in a voice channel!");
                return;
            }

            var juke = await JukeboxService.GetJukeboxAsync(Context.Guild);

            IUserMessage? msg = null;
            await juke.PlayAsync(await GetRequestAsync(mediaQuery), GetUserVoiceChannel(), true, new JukeboxPlayer.StatusCallbacks()
            {
                downloadingCallback = async (media) =>
                {
                    msg = await ReplyAsync(null, false, GetMediaEmbed("**Downloading...**", media, Color.Blue));
                },
                playingCallback = async (media, queued) =>
                {
                    await msg!.ModifyAsync(x => x.Embed = GetMediaEmbed("**Now Playing**", media, Color.Green));
                }
            });
        }

        [Command("Play"), Alias("P"), Summary("Plays the specified song.")]
        public async Task PlayAsync([Remainder] string? mediaQuery = null)
        {
            if (GetUserVoiceChannel() == null)
            {
                await ReplyAsync("You need to be in a voice channel!");
                return;
            }

            if (mediaQuery == null && Context.Message.Attachments.Count == 0)
            {
                await ReplyAsync("You need to specify a url, search query or upload a file.");
                return;
            }

            var juke = await JukeboxService.GetJukeboxAsync(Context.Guild);

            IUserMessage? msg = null;
            await juke.PlayAsync(await GetRequestAsync(mediaQuery!), GetUserVoiceChannel(), false, new JukeboxPlayer.StatusCallbacks()
            {
                downloadingCallback = async (media) =>
                {
                    msg = await ReplyAsync(null, false, GetMediaEmbed("**Downloading...**", media, Color.Blue));
                },
                playingCallback = async (media, queued) =>
                {
                    await msg!.ModifyAsync(x => x.Embed = GetMediaEmbed("**Now Playing**", media, Color.Green));                   
                }
            });
        }

        [Command("Stop"), Summary("Stops playback.")]
        public async Task StopAsync()
        {
            if (!(await JukeboxService.GetJukeboxAsync(Context.Guild)).Playing)
            {
                await ReplyAsync("No song is playing.");
                return;
            }
            await (await JukeboxService.GetJukeboxAsync(Context.Guild)).StopAsync();

            await ReplyAsync("Stopped playback.");
        }
    }
}