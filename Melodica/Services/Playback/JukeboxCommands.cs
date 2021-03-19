using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Discord;
using Discord.Commands;
using Discord.WebSocket;

using Melodica.Services.Caching;
using Melodica.Services.Downloaders;
using Melodica.Services.Media;
using Melodica.Services.Playback.Exceptions;
using Melodica.Services.Playback.Requests;
using Melodica.Utility.Extensions;

namespace Melodica.Services.Playback
{
    public class JukeboxCommands : ModuleBase<SocketCommandContext>
    {
        Jukebox? cachedJukebox;
        private Jukebox Jukebox => cachedJukebox ??= JukeboxManager.GetOrCreateJukeboxAsync(Context.Guild, () => new Jukebox(MediaCallback)).GetAwaiter().GetResult();

        private readonly ManualResetEventSlim mediaCallbackLock = new(true);
        private IUserMessage? lastPlayMessage;

        private static Color MediaStateToColor(MediaState state) => state switch
        {
            MediaState.Error => Color.Red,
            MediaState.Queued => Color.DarkGrey,
            MediaState.Downloading => Color.Blue,
            MediaState.Playing => Color.Green,
            MediaState.Finished => Color.LighterGrey,
            _ => Color.Default,
        };

        private static Embed CreateMediaEmbed(MediaInfo info, MediaInfo? playlistInfo, MediaState state)
        {
            const char InfChar = '\u221E';

            var color = MediaStateToColor(state);

            var description = playlistInfo != null ? $"__{info.Title}__\n{playlistInfo.Title}" : info.Title;

            bool isLive = info.MediaType == MediaType.Livestream;
            var footer = isLive ? InfChar.ToString() : $"{info.Duration}{(playlistInfo is not null ? $" | {playlistInfo.Duration}" : "")}";

            var embed = new EmbedBuilder()
                        .WithColor(color)
                        .WithTitle(info.Artist)
                        .WithDescription(description)
                        .WithFooter(footer)
                        .WithThumbnailUrl(info.ImageUrl)
                        .Build();
            return embed;
        }

        async ValueTask MediaCallback(MediaInfo info, MediaInfo? playlistInfo, MediaState state)
        {
            if (info is null)
            {
                await ReplyAsync("Info was null in MediaCallback. (dbg)");
                return;
            }

            try
            {
                mediaCallbackLock.Wait();
                mediaCallbackLock.Reset();

                if (info.MediaType == MediaType.Playlist)
                {
                    var plEmbed = CreateMediaEmbed(info, null, MediaState.Queued);
                    await ReplyAsync(null, false, plEmbed);
                    return;
                }

                var embed = CreateMediaEmbed(info, playlistInfo, state);

                if (state == MediaState.Downloading || state == MediaState.Queued)
                {
                    lastPlayMessage = await ReplyAsync(null, false, embed);
                }

                if (lastPlayMessage is null)
                {
                    await ReplyAsync(null, false, embed);
                    return;
                }

                await lastPlayMessage.ModifyAsync(x => x.Embed = embed);

                if (state == MediaState.Finished || state == MediaState.Error)
                {
                    lastPlayMessage = null;
                }
            }
            finally
            {
                mediaCallbackLock.Set();
            }
        }

        private IVoiceChannel GetUserVoiceChannel() => ((SocketGuildUser)Context.User).VoiceChannel;

        private Task<IMediaRequest> GetRequestAsync(string query)
        {
            var attach = Context.Message.Attachments;
            if (attach.Count != 0)
            {
                return Task.FromResult(new AttachmentMediaRequest(attach.ToArray()) as IMediaRequest);

            }
            var downloader = DownloaderResolver.GetDownloaderFromQuery(query) ?? (query.IsUrl() ? null : IAsyncDownloader.Default);
            IMediaRequest request = downloader == null ? new URLMediaRequest(null, query, true) : new DownloadRequest(query!, downloader);
            return Task.FromResult(request);
        }

        [Command("ClearCache"), Summary("Clears cache."), RequireOwner]
        public async Task ClearCacheAsync()
        {
            var (deletedFiles, filesInUse, ms) = await MediaFileCache.ClearAllCachesAsync();
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
            var media = Jukebox.GetSong();
            if (media is null)
            {
                await ReplyAsync("GetSong returned null.");
                return;
            }
            var embed = CreateMediaEmbed(media.Info, media.CollectionInfo, MediaState.Queued);
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

            var dur = Jukebox.Elapsed;
            var song = Jukebox.GetSong();
            if (song is null)
            {
                await ReplyAsync("Could not get song from jukebox.");
                return;
            }
            var songDur = song.Info.Duration;
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
            var queue = Jukebox.GetQueue();
            // If index is null (default) then remove the last element.
            var removed = index == null ? await queue.RemoveAtAsync(^0) : await queue.RemoveAtAsync(index.Value - 1);
            var removedInfo = removed.Info;
            await ReplyAsync(null, false, new EmbedBuilder()
            {
                Title = "**Removed**",
                Description = removedInfo.Title
            }.Build());
        }

        [Command("Queue"), Summary("Shows current queue.")]
        public async Task QueueAsync()
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
                var (queueDuration, imageUrl) = await queue.GetQueueInfo();
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

            var request = await GetRequestAsync(query);

            // Get info to see if the request is actually valid.
            var info = await request.GetInfoAsync();

            Jukebox.Shuffle = false;
            await Jukebox.SetNextAsync(request);
            await ReplyAsync(null, false, CreateMediaEmbed(info, null, MediaState.Queued));
        }

        [Command("Switch"), Summary("Changes the current song.")]
        public async Task SwitchAsync([Remainder] string? mediaQuery = null)
        {
            var userVoice = GetUserVoiceChannel();
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
                var jukebox = Jukebox;
                if (jukebox.Playing)
                {
                    await jukebox.SwitchAsync(await GetRequestAsync(mediaQuery!), userVoice);
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
            var userVoice = GetUserVoiceChannel();
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

        // Used to play an audio file on the server. Mainly used when youtube is down.
        [Command("PlayLocal"), RequireOwner]
        public async Task PlayLocalMedia([Remainder] string directUrl)
        {
            var userVoice = GetUserVoiceChannel();

            GuildPermissionsChecker.AssertVoicePermissions(Context.Guild, Context.Client.CurrentUser, userVoice);

            var req = new LocalMediaRequest(directUrl);
            await Jukebox.PlayAsync(req, userVoice);
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
}