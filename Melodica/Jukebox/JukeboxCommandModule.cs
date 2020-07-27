using Melodica.Jukebox;
using Melodica.Jukebox.Services;
using Melodica.Jukebox.Models.Requests;
using Melodica.Jukebox.Services.Downloaders;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Melodica.Utility.Extensions;
using Melodica.Jukebox.Models;
using System.Collections.Generic;
using System.Threading;
using Melodica.Jukebox.Models.Exceptions;

namespace Melodica.Jukebox
{
    //[Group("Jukebox"), Alias("J")]
    public class JukeboxCommandModule : ModuleBase<SocketCommandContext>
    {
        public JukeboxCommandModule()
        {
        }

        Embed? playbackEmbed;
        IUserMessage? playbackMessage;
        IUserMessage? playbackPlaylistMessage;
        readonly SemaphoreSlim playbackLock = new SemaphoreSlim(1);

        private async void PlaybackCallback((MediaMetadata info, SubRequestInfo? subInfo, MediaState state) ctx)
        {
            await playbackLock.WaitAsync(); // Use a this to make sure no threads send multiple messages at the same time.    

            bool reset = false;

            Embed OnDone()
            {
                reset = true;
                return CreateMediaEmbed("Done", ctx.info, ctx.subInfo, Color.LighterGrey);
            }

            Embed OnUnavailable()
            {
                reset = true;
                return CreateMediaEmbed("**Unavailable**", ctx.info, ctx.subInfo, Color.Red);
            }

            playbackEmbed = ctx.state switch
            {
                MediaState.Error => OnUnavailable(),
                MediaState.Downloading => CreateMediaEmbed("**Downloading**", ctx.info!, ctx.subInfo, Color.Blue),
                MediaState.Queued => CreateMediaEmbed("**Queued**", ctx.info!, ctx.subInfo, null, null, ctx.info.MediaType == MediaType.Livestream ? '\u221E'.ToString() : null),
                MediaState.Playing => ctx.info.MediaType switch
                {
                    MediaType.Video => CreateMediaEmbed("**Playing**", ctx.info!, ctx.subInfo, Color.Green),
                    MediaType.Playlist => CreateMediaEmbed("**Playing**", ctx.info!, ctx.subInfo, Color.Green),
                    MediaType.Livestream => CreateMediaEmbed("**Streaming**", ctx.info!, ctx.subInfo, Color.DarkGreen, null, '\u221E'.ToString()),
                    _ => throw new Exception("Unknown error in PlaybackCallback switch expression"),
                },
                MediaState.Finished => OnDone(),
                _ => throw new Exception("Unknown error in PlaybackCallback"),
            };

            if (playbackMessage == null)
            {
                if (ctx.info.MediaType == MediaType.Playlist)
                {
                    if (playbackPlaylistMessage == null)
                        playbackPlaylistMessage = await ReplyAsync(null, false, playbackEmbed);
                    else
                        await playbackPlaylistMessage.ModifyAsync(x => x.Embed = playbackEmbed);
                }
                else
                    playbackMessage = await ReplyAsync(null, false, playbackEmbed);
            }
            else
            {
                await playbackMessage.ModifyAsync(x => x.Embed = playbackEmbed);
            }

            if (reset)
            {
                playbackMessage = null;
                reset = false;
            }

            playbackLock.Release();
        }

        private IVoiceChannel GetUserVoiceChannel() => ((SocketGuildUser)Context.User).VoiceChannel;

        private Embed CreateMediaEmbed(string embedTitle, MediaMetadata mediaInfo, SubRequestInfo? subInfo, Color? color = null, string? description = null, string? footerText = null)
        {
            return new EmbedBuilder()
                   .WithColor(color ?? Color.DarkGrey)
                   .WithTitle(embedTitle)
                   .WithDescription(subInfo.HasValue ? $"__{description ?? mediaInfo.Title}__\n{subInfo.Value.ParentRequestInfo!.Title}" : description ?? mediaInfo.Title)
                   .WithFooter(mediaInfo.Duration != TimeSpan.Zero ?
                               subInfo.HasValue ?
                               $"{mediaInfo.Duration} | {subInfo.Value.ParentRequestInfo!.Duration}" :
                               footerText ?? mediaInfo.Duration.ToString() : "")
                   .WithThumbnailUrl(mediaInfo.Thumbnail).Build();
        }

        private Task<MediaRequestBase> GetRequestAsync(string query)
        {
            var attach = Context.Message.Attachments;
            if (attach.Count != 0)
            {
                return Task.FromResult(new AttachmentMediaRequest(attach.ToArray()) as MediaRequestBase);
            }
            else
            {
                var downloader = DownloaderResolver.GetDownloaderFromURL(query) ?? (query.IsUrl() ? null : AsyncDownloaderBase.Default);
                return Task.FromResult(downloader == null ? new URLMediaRequest(null, query, true) : new DownloadRequest(query!, downloader) as MediaRequestBase);
            }
        }

        private void CheckForPermissions(IVoiceChannel voice)
        {
            var botGuildRoles = Context.Guild.GetUser(Context.Client.CurrentUser.Id).Roles;
            foreach (var role in botGuildRoles) // Check through all roles.
            {
                if (role.Permissions.Administrator)
                    return;
                var guildRolePerms = voice.GetPermissionOverwrite(role);
                if (guildRolePerms == null)
                    continue;
                var allowedGuildPerms = guildRolePerms!.Value.ToAllowList();
                if (allowedGuildPerms.Contains(ChannelPermission.Connect) && allowedGuildPerms.Contains(ChannelPermission.Speak))
                    return;
            }

            // Check for user role.
            var botPerms = voice.GetPermissionOverwrite(Context.Client.CurrentUser);
            if (botPerms != null)
            {
                var allowedBotPerms = botPerms!.Value.ToAllowList();
                if (allowedBotPerms.Contains(ChannelPermission.Connect) && allowedBotPerms.Contains(ChannelPermission.Speak))
                    return;
            }
            throw new Exception("I don't have explicit permission to connect and speak in this channel :(");
        }

        [Command("ClearCache"), Summary("Clears cache."), RequireOwner]
        public async Task ClearCacheAsync()
        {
            var (deletedFiles, filesInUse, ms) = await MediaCache.PruneAllCachesAsync();
            await ReplyAsync($"Deleted {deletedFiles} files. ({filesInUse} files in use) [{ms}ms]");
        }

        [Command("Shuffle"), Summary("Toggles shuffle.")]
        public async Task ShuffleAsync()
        {
            var juke = await JukeboxManager.GetJukeboxAsync(Context.Guild);
            bool state = juke.Shuffle = !juke.Shuffle;
            await ReplyAsync($"Shuffle {(state ? "On" : "Off")}");
        }

        [Command("Repeat"), Summary("Toggles repeat of the queue.")]
        public async Task ToggleRepeatAsync()
        {
            var juke = await JukeboxManager.GetJukeboxAsync(Context.Guild);
            bool state = juke.Repeat = !juke.Repeat;
            await ReplyAsync($"Repeat {(state ? "On" : "Off")}");
        }

        [Command("Loop"), Summary("Toggles loop on the current song.")]
        public async Task SetLoopingAsync([Remainder] string? query = null)
        {
            var juke = await JukeboxManager.GetJukeboxAsync(Context.Guild);

            if (!string.IsNullOrEmpty(query))
            {
                await InternalPlayAsync(query, true, true);
                return;
            }

            bool state = juke.Loop = !juke.Loop;
            await ReplyAsync($"Loop {(state ? "On" : "Off")}");
        }

        [Command("Song"), Alias("Info", "SongInfo"), Summary("Gets info about the current song.")]
        public async Task GetSongAsync()
        {
            var juke = await JukeboxManager.GetJukeboxAsync(Context.Guild);
            if (!juke.Playing)
            {
                await ReplyAsync("No song is playing.");
                return;
            }
            await ReplyAsync(null, false, playbackEmbed!);
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
            if (index <= 0)
            {
                await ReplyAsync("The index to remove cannot be under 0.");
                return;
            }

            var removed = (await JukeboxManager.GetJukeboxAsync(Context.Guild)).RemoveFromQueue(index - 1);
            await ReplyAsync(null, false, CreateMediaEmbed("Removed", removed, null));
        }

        [Command("RemoveLast"), Summary("Removes the last song from the queue.")]
        public async Task RemoveLast() 
        { 
            var removed = (await JukeboxManager.GetJukeboxAsync(Context.Guild)).RemoveFromQueue(^0);
            await ReplyAsync(null, false, CreateMediaEmbed("Removed", removed, null));
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
                var queueDuration = queue.GetTotalDuration();
                eb.WithTitle("**Queue**")
                  .WithThumbnailUrl(queue.GetMediaInfo().Thumbnail)
                  .WithFooter($"{(queueDuration == TimeSpan.Zero ? '\u221E'.ToString() : queueDuration.ToString())}{(juke.Shuffle ? " | Shuffle" : "")}");

                int maxElems = 20;
                for (int i = 1; i <= maxElems; i++)
                {
                    if (i > queue.Length)
                        break;
                    var x = queue[i - 1];
                    eb.AddField(i == 1 ? "Next:" : i == maxElems ? "And more" : i.ToString(), i == 1 ? $"**{x.GetInfo().Title}**" : i == maxElems ? $"Plus {queue.Length - (i - 1)} other songs!" : x.GetInfo().Title, false);
                }
            }

            await Context.Channel.SendMessageAsync(null, false, eb.Build());
        }

        private async Task InternalPlayAsync(string? query, bool switchPlayback, bool loop)
        {
            var userVoice = GetUserVoiceChannel();
            if (userVoice == null)
            {
                await ReplyAsync("You need to be in a voice channel!");
                return;
            }

            CheckForPermissions(userVoice);

            if (query == null && Context.Message.Attachments.Count == 0)
            {
                await ReplyAsync("You need to specify a url, search query or upload a file.");
                return;
            }

            var juke = await JukeboxManager.GetJukeboxAsync(Context.Guild);

            try { await juke.PlayAsync(await GetRequestAsync(query!), userVoice, switchPlayback, loop, PlaybackCallback); }
            catch (EmptyChannelException) { await ReplyAsync("All users have left the channel. Disconnecting..."); }
        }

        [Command("Switch"), Summary("Changes the current song.")]
        public Task SwitchAsync([Remainder] string? mediaQuery = null)
        {
            return InternalPlayAsync(mediaQuery, true, false);
        }

        [Command("Play"), Summary("Plays the specified song.")]
        public Task PlayAsync([Remainder] string? mediaQuery = null)
        {
            return InternalPlayAsync(mediaQuery, false, false);
        }

        // Used to play an audio file on the server. Mainly used when youtube is down.
        [Command("PlayLocal"), RequireOwner]
        public async Task PlayLocalMedia([Remainder] string directUrl)
        {
            var userVoice = GetUserVoiceChannel();
            CheckForPermissions(userVoice);

            var juke = await JukeboxManager.GetJukeboxAsync(Context.Guild);
            var req = new LocalMediaRequest(directUrl);
            await juke.PlayAsync(req, userVoice, true, false, PlaybackCallback);
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
        }
    }
}