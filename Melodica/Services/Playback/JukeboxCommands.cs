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
        private Jukebox Jukebox => JukeboxManager.GetOrCreateJukeboxAsync(Context.Guild, () => new Jukebox(MediaCallback)).GetAwaiter().GetResult();

        private Embed? playbackEmbed;
        private IUserMessage? playbackMessage;
        private IUserMessage? playbackPlaylistMessage;
        private readonly SemaphoreSlim playbackLock = new(1);

        private async void MediaCallback(MediaInfo info, MediaState state, MediaInfo? parentInfo)
        {
            await playbackLock.WaitAsync(); // Use a this to make sure no threads send multiple messages at the same time.

            bool reset = false;

            Embed OnDone()
            {
                reset = true;
                return CreateMediaEmbed(info, parentInfo, Color.LighterGrey);
            }

            Embed OnUnavailable()
            {
                reset = true;
                return CreateMediaEmbed(info, parentInfo, Color.Red);
            }

            var newEmbed = state switch
            {
                MediaState.Error => OnUnavailable(),
                MediaState.Downloading => CreateMediaEmbed(info!, parentInfo, Color.Blue),
                MediaState.Queued => CreateMediaEmbed(info!, parentInfo, null, info.MediaType == MediaType.Livestream ? '\u221E'.ToString() : null),
                MediaState.Playing => info.MediaType switch
                {
                    MediaType.Video => CreateMediaEmbed(info!, parentInfo, Color.Green),
                    MediaType.Playlist => CreateMediaEmbed(info!, parentInfo, Color.Green),
                    MediaType.Livestream => CreateMediaEmbed(info!, parentInfo, Color.DarkGreen, '\u221E'.ToString()),
                    _ => throw new Exception("Unknown error in PlaybackCallback switch expression"),
                },
                MediaState.Finished => OnDone(),
                _ => throw new Exception("Unknown error in PlaybackCallback"),
            };

            if (playbackMessage == null)
            {
                if (info.MediaType == MediaType.Playlist)
                {
                    if (playbackPlaylistMessage == null)
                        playbackPlaylistMessage = await ReplyAsync(null, false, newEmbed);
                    else
                        await playbackPlaylistMessage.ModifyAsync(x => x.Embed = newEmbed);
                }
                else
                {
                    playbackMessage = await ReplyAsync(null, false, newEmbed);
                }
            }
            else
            {
                if (state == MediaState.Queued)
                {
                    await ReplyAsync(null, false, newEmbed);
                    playbackLock.Release();
                    return;
                }
                else
                {
                    await playbackMessage.ModifyAsync(x => x.Embed = newEmbed);
                }
            }

            playbackEmbed = newEmbed;

            if (reset)
            {
                playbackMessage = null;
                reset = false;
            }

            playbackLock.Release();
        }

        private IVoiceChannel GetUserVoiceChannel() => ((SocketGuildUser)Context.User).VoiceChannel;

        private static Embed CreateMediaEmbed(MediaInfo mediaInfo, MediaInfo? collectionInfo, Color? color = null, string? footerText = null)
        {
            color ??= Color.DarkGrey;
            var footer = mediaInfo?.Duration != TimeSpan.Zero ?
                         collectionInfo != null ?
                         $"{mediaInfo?.Duration} | {collectionInfo?.Duration}" :
                         footerText ?? mediaInfo?.Duration.ToString() : "";
            var embed = new EmbedBuilder()
                        .WithColor(color.Value)
                        .WithTitle(mediaInfo?.Artist ?? "Unknown")
                        .WithDescription(collectionInfo != null ? $"__{mediaInfo?.Title}__\n{collectionInfo.Title}" : mediaInfo?.Title)
                        .WithFooter(footer)
                        .WithThumbnailUrl(mediaInfo?.ImageUrl).Build();
            return embed;
        }

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

        [Command("Repeat"), Summary("Toggles repeat of the queue.")]
        public async Task ToggleRepeatAsync()
        {
            bool state = Jukebox.Repeat = !Jukebox.Repeat;
            await ReplyAsync($"Repeat {(state ? "On" : "Off")}");
        }

        [Command("Loop"), Summary("Toggles loop on the current song.")]
        public async Task SetLoopingAsync()
        {
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
            await ReplyAsync(null, false, playbackEmbed!);
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
            if(song is null)
            {
                await ReplyAsync("Could not get song from jukebox.");
                return;
            }
            var songDur = song.Value.song.Duration;
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
                var queueDuration = await queue.GetTotalDurationAsync();
                var info = await queue.GetMediaInfoAsync();
                eb.WithTitle("**Queue**")
                  .WithThumbnailUrl(info.ImageUrl)
                  .WithFooter($"{(queueDuration == TimeSpan.Zero ? '\u221E'.ToString() : queueDuration.ToString())}{(Jukebox.Shuffle ? " | Shuffle" : "")}");

                int maxElems = 20;
                for (int i = 1; i <= maxElems; i++)
                {
                    if (i > queue.Length)
                        break;
                    var song = queue[i - 1];
                    var songInfo = song.Info;
                    eb.AddField(i == 1 ? "Next:" : i == maxElems ? "And more" : i.ToString(), i == 1 ? $"**{songInfo.Artist} - {songInfo.Title}**" : i == maxElems ? $"Plus {queue.Length - (i - 1)} other songs!" : $"{songInfo.Artist} - {songInfo.Title}", false);
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
            await ReplyAsync(null, false, CreateMediaEmbed(info, null));
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