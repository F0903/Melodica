using CasinoBot.Modules.Jukebox;
using CasinoBot.Modules.Jukebox.Services.Cache;
using CasinoBot.Modules.Jukebox.Services.Downloaders;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using System.Linq;
using System.Threading.Tasks;

namespace CasinoBot.Modules.Jukebox
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

        [Command("jOk")]
        public async Task IsWorking()
        {
            await ReplyAsync("Jukebox status OK");
        }

        [Command("Shuffle"), Summary("Shuffles the queue.")]
        public async Task SetShuffleAsync(bool val)
        {
            (await jukebox.GetJukeboxAsync(Context.Guild)).Shuffle = true;
            await ReplyAsync($"Shuffle set to {val}");
        }

        [Command("IsLooping"), Summary("")]
        public async Task IsLoopingAsync()
        {
            await ReplyAsync($"Loop is set to {(await jukebox.GetJukeboxAsync(Context.Guild)).Looping}");
        }

        [Command("Loop"), Summary("Loops the current song.")]
        public async Task SetLoopingAsync(bool? val = null)
        {
            var juke = await jukebox.GetJukeboxAsync(Context.Guild);
            val ??= !juke.Playing;
            juke.Looping = val.Value;
            await ReplyAsync($"Loop set to {val}");
        }

        [Command("Song"), Summary("Gets the currently playing song.")]
        public async Task GetSongAsync()
        {
            await ReplyAsync($"**Currently playing** {(await jukebox.GetJukeboxAsync(Context.Guild)).CurrentSong}");
        }

        [Command("Join"), Summary("Joins the specified voice channel, or the channel the calling user is in.")]
        public async Task JoinAsync(string channelName = null)
        {
            await (await jukebox.GetJukeboxAsync(Context.Guild)).ConnectToChannelAsync(channelName == null ? GetUserVoiceChannel() : Context.Guild.VoiceChannels.Single(x => x.Name == channelName));
        }

        [Command("Leave"), Summary("Leaves the voice channel.")]
        public async Task LeaveAsync()
        {
            await (await jukebox.GetJukeboxAsync(Context.Guild)).LeaveChannelAsync();
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
            await ReplyAsync($"Removed {removed.Title} from queue.");
        }

        [Command("Queue"), Summary("Shows current queue.")]
        public async Task QueueAsync()
        {
            var juke = await jukebox.GetJukeboxAsync(Context.Guild);

            Models.PlayableMedia[] queue = null;
            try { queue = juke.GetQueue(); } catch { }
            if (queue != null && queue.Length == 0)
                queue = null;

            if (queue == null)
            {
                await ReplyAsync("No songs queued.");
                return;
            }

            EmbedBuilder eb = new EmbedBuilder
            {
                Color = Color.DarkGrey
            };

            eb.WithTitle("**Queue**");

            for (int i = 1; i <= 10; i++)
            {
                if (i >= queue.Length)
                    break;
                var x = queue[i - 1];
                eb.AddField(i == 1 ? "Next:" : i == 10 ? "And more" : i.ToString(), i == 1 ? $"**{x.Title}**" : i == 10 ? $"Plus {queue.Length - (i - 1)} other songs!" : x.Title, false);
            }

            eb.WithFooter($"Shuffle - {(juke.Shuffle ? "On" : "Off")}");

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

            var loop = songQuery.EndsWith(" !loop");
            if (loop)
                songQuery = songQuery.Replace(" !loop", null);

            if (juke.Playing)
                await juke.StopAsync();

            await juke.PlayAsync(new Models.Requests.QueryDownloadRequest(IoC.Kernel.Get<IAsyncDownloadService>(), songQuery), GetUserVoiceChannel(), async context => await ReplyAsync($"**Switched Playback** {context.song}"));
        }

        [Command("Play"), Alias("P"), Summary("Plays the specified song.")]
        public async Task PlayAsync([Remainder] string songQuery)
        {
            if (GetUserVoiceChannel() == null)
            {
                await ReplyAsync("You need to be in a voice channel!");
                return;
            }

            var loop = songQuery.EndsWith(" !loop");
            if (loop)
                songQuery = songQuery.Replace(" !loop", null);

            var juke = await jukebox.GetJukeboxAsync(Context.Guild);

            await juke.PlayAsync(new Models.Requests.QueryDownloadRequest(IoC.Kernel.Get<IAsyncDownloadService>(), songQuery), GetUserVoiceChannel(), async (context) =>
            {
                await ReplyAsync($"{(context.queued ? "**Queued**" : "**Now Playing**")} {context.song}");
                juke.Looping = loop;
            },
            async () => await ReplyAsync("Large media detected. This might take a bit."),
            async (song) => await ReplyAsync($"{song} was unavailable. Skipping..."));
        }

        [Command("Stop"), Summary("Stops playback.")]
        public async Task StopAsync()
        {
            await (await jukebox.GetJukeboxAsync(Context.Guild)).StopAsync();
            await ReplyAsync("Stopped playback.");
        }
    }
}