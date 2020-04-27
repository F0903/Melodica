using Suits.Jukebox;
using Suits.Jukebox.Services;
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

            downloader.VideoUnavailableCallback = MediaUnavailableCallback;
        }

        private readonly AsyncYoutubeDownloader downloader; // Temp until a better solution is figured out for more platforms

        //TODO: Reimplement the downloading message.
        private Jukebox.StatusCallbacks StatusCallbacks(bool looping = false)
        {
            return new Jukebox.StatusCallbacks()
            {
                playingCallback = async (ctx) =>
                {
                    if (ctx.state.queued)
                    {
                        await ReplyAsync(null, false, GetMediaEmbed("**Queued**", ctx.infoSet?.songInfo!));
                        return;
                    }

                    switch (ctx.type)
                    {
                        case MediaType.Video:
                            await ReplyAsync(null, false, GetMediaEmbed(looping ? "**Looping**" : "**Playing**", ctx.infoSet?.songInfo!, Color.Green));
                            break;
                        case MediaType.Playlist:
                            await ReplyAsync(null, false, GetMediaEmbed("**Playing**", ctx.infoSet?.songInfo!, Color.Green));
                            break;
                        case MediaType.Livestream:
                            await ReplyAsync(null, false, GetMediaEmbed("**Streaming**", ctx.infoSet?.songInfo!, Color.Orange, null, '\u221E'.ToString()));
                            break;
                        default:
                            break;
                    }
                }
            };
        }

        private IVoiceChannel GetUserVoiceChannel() => ((SocketGuildUser)Context.User).VoiceChannel;

        private Embed GetMediaEmbed(string embedTitle, Metadata mediaInfo, Color? color = null, string? description = null, string? footerText = null)
        {
            return new EmbedBuilder().WithColor(color ?? Color.Default)
                                     .WithTitle(embedTitle)
                                     .WithDescription(description ?? mediaInfo.Title)
                                     .WithFooter(footerText ?? mediaInfo.Duration.ToString())
                                     .WithThumbnailUrl(mediaInfo.Thumbnail).Build();
        }

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

        private void CheckForPermissions(IVoiceChannel voice)
        {
            var botRoles = Context.Guild.GetUser(Context.Client.CurrentUser.Id).Roles;
            foreach (var role in botRoles) // Check through all roles.
            {
                if (role.Permissions.Administrator)
                    return;
                var perms = voice.GetPermissionOverwrite(role);
                if (perms == null)
                    continue;
                var allowedPerms = perms!.Value.ToAllowList();
                if (allowedPerms.Contains(ChannelPermission.Connect) && allowedPerms.Contains(ChannelPermission.Speak))
                    return;
            }

            // Check for user role.
            var userPerms = voice.GetPermissionOverwrite(Context.Client.CurrentUser);
            var allowedUserPerms = userPerms!.Value.ToAllowList();
            if (allowedUserPerms.Contains(ChannelPermission.Connect) && allowedUserPerms.Contains(ChannelPermission.Speak))
                return;

            throw new Exception("I'm not allowed to connect or speak in this channel :(");
        }

        [Command("ClearCache"), Summary("Clears cache."), RequireOwner]
        public async Task ClearCacheAsync()
        {
            var (deletedFiles, filesInUse, ms) = await MediaCache.PruneCacheAsync(true);
            await ReplyAsync($"Deleted {deletedFiles} files. ({filesInUse} files in use) [{ms}ms]");
        }

        [Command("Shuffle"), Summary("Toggles shuffle.")]
        public async Task ShuffleAsync()
        {
            var juke = await JukeboxManager.GetJukeboxAsync(Context.Guild);
            await juke.ToggleShuffleAsync(async (info, wasShuffling) => await ReplyAsync(null, false, GetMediaEmbed(wasShuffling ? "**Stopped Shuffling**" : "**Shuffling**", info)));
        }

        [Command("Loop"), Summary("Toggles loop on the current song.")]
        public async Task SetLoopingAsync([Remainder] string? query = null)
        {
            var juke = await JukeboxManager.GetJukeboxAsync(Context.Guild);

            if (!string.IsNullOrEmpty(query))
                await juke.PlayAsync(new DownloadRequest<AsyncYoutubeDownloader>(query), GetUserVoiceChannel(), true, true, StatusCallbacks(true));
            else
                await juke.ToggleLoopAsync(async ctx => await ReplyAsync(null, false, GetMediaEmbed(ctx.wasLooping ? "**Stopped Looping**" : "**Looping**", ctx.info)));
        }

        [Command("Song"), Alias("Info", "SongInfo"), Summary("Gets info about the current song.")]
        public async Task GetSongAsync()
        {
            var song = (await JukeboxManager.GetJukeboxAsync(Context.Guild)).CurrentSong;
            if (song == null)
            {
                await ReplyAsync("No song is playing.");
                return;
            }
            await ReplyAsync(null, false, GetMediaEmbed("**Playing**", song.Info));
        }

        [Command("Resume"), Summary("Resumes playback.")]
        public async Task ResumeAsync()
        {
            (await JukeboxManager.GetJukeboxAsync(Context.Guild)).Paused = false;
        }

        [Command("Pause"), Alias("Unpause"), Summary("Pauses playback.")]
        public async Task PauseAsync()
        {
            (await JukeboxManager.GetJukeboxAsync(Context.Guild)).Paused = true;
        }

        [Command("Skip"), Summary("Skips current song.")]
        public async Task SkipAsync()
        {
            (await JukeboxManager.GetJukeboxAsync(Context.Guild)).Skip();
        }

        [Command("Clear"), Summary("Clears queue.")]
        public async Task ClearQueue()
        {
            await (await JukeboxManager.GetJukeboxAsync(Context.Guild)).ClearQueueAsync();
            await ReplyAsync("Cleared queue.");
        }

        [Command("Remove"), Summary("Removes song from queue by index.")]
        public async Task RemoveSongFromQueue(int index)
        {
            if (index >= 0)
            {
                await ReplyAsync("The index to remove cannot be under 0.");
                return;
            }
            
            var removed = (await JukeboxManager.GetJukeboxAsync(Context.Guild)).RemoveFromQueue(index - 1);
            await ReplyAsync(null, false, GetMediaEmbed("Removed", removed));
        }

        [Command("Queue"), Summary("Shows current queue.")]
        public async Task QueueAsync()
        {
            var juke = await JukeboxManager.GetJukeboxAsync(Context.Guild);

            var queue = juke.GetQueue();

            EmbedBuilder eb = new EmbedBuilder();
            if (queue.IsEmpty)
            {
                eb.WithTitle("**Queue**")
                  .WithDescription("No songs are queued.")
                  .WithFooter("It's quite empty down here...");
            }
            else
            {
                eb.WithTitle("**Queue**")
                  .WithThumbnailUrl(queue.GetMediaInfo().Thumbnail)
                  .WithFooter($"Duration - {queue.GetTotalDuration()}{(juke.Shuffle ? "| Shuffle" : "")}");

                int maxElems = 20;
                for (int i = 1; i <= maxElems; i++)
                {
                    if (i > queue.Length)
                        break;
                    var x = queue[i - 1];
                    eb.AddField(i == 1 ? "Next:" : i == maxElems ? "And more" : i.ToString(), i == 1 ? $"**{x.GetMediaInfo().Title}**" : i == maxElems ? $"Plus {queue.Length - (i - 1)} other songs!" : x.GetMediaInfo().Title, false);
                }
            }

            await Context.Channel.SendMessageAsync(null, false, eb.Build());
        }

        // Move this to a service.
        private async Task PlayMediaAsync(string? query, bool switchPlayback)
        {
            var userVoice = GetUserVoiceChannel();
            if (userVoice == null)
            {
                await ReplyAsync("You need to be in a voice channel!");
                return;
            }
           
            // Check if we are able to connect.
            CheckForPermissions(userVoice);

            if (query == null && Context.Message.Attachments.Count == 0)
            {
                await ReplyAsync("You need to specify a url, search query or upload a file.");
                return;
            }

            var juke = await JukeboxManager.GetJukeboxAsync(Context.Guild);

            await juke.PlayAsync(await GetRequestAsync(query!), userVoice, switchPlayback, false, StatusCallbacks());
        }

        [Command("Switch"), Alias("Change"), Summary("Changes the current song.")]
        public Task SwitchAsync([Remainder] string? mediaQuery = null)
        {
            return PlayMediaAsync(mediaQuery, true);
        }

        [Command("Play"), Alias("P"), Summary("Plays the specified song.")]
        public Task PlayAsync([Remainder] string? mediaQuery = null)
        {
            return PlayMediaAsync(mediaQuery, false);
        }

        [Command("Stop"), Summary("Stops playback.")]
        public async Task StopAsync()
        {
            var juke = await JukeboxManager.GetJukeboxAsync(Context.Guild);
            if (!juke.Playing)
            {
                await ReplyAsync("No song is playing.");
                return;
            }
            await juke.StopAsync();

            await ReplyAsync("Stopped playback.");
        }
    }
}