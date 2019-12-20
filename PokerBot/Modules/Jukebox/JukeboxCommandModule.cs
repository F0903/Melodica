using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Discord.Audio;
using PokerBot.Utility.Extensions;
using System.Threading.Tasks;

namespace PokerBot.Modules.Jukebox
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

        [Command("jWorking")]
        public async Task IsWorking()
        {
            await ReplyAsync("Jukebox status OK");
        }

        [Command("Loop"), Summary("Loops the current song.")]
        public async Task SetLoopingAsync(bool? val = null)
        {
            val ??= !jukebox.IsLooping(Context.Guild);
            jukebox.SetLooping(Context.Guild, val.Value);
            await ReplyAsync($"Loop set to {val}");
        }

        [Command("Song"), Summary("Gets the currently playing song.")]
        public async Task GetSongAsync()
        {
            await ReplyAsync($"**Currently playing** {jukebox.GetPlayingSong(Context.Guild)}");
        }

        [Command("Join"), Summary("Joins the specified voice channel, or the channel the calling user is in.")]
        public async Task JoinAsync(string channelName = null)
        {
            await jukebox.JoinChannelAsync(Context.Guild, channelName == null ? GetUserVoiceChannel() : Context.Guild.VoiceChannels.Single(x => x.Name == channelName));
        }

        [Command("Leave"), Summary("Leaves the voice channel.")]
        public async Task LeaveAsync()
        {
            await jukebox.LeaveChannelAsync(Context.Guild);
        }

        [Command("Resume"), Summary("Resumes playback.")]
        public Task ResumeAsync()
        {
            jukebox.SetPause(Context.Guild, false);
            return Task.CompletedTask;
        }

        [Command("Pause"), Summary("Pauses playback or sets the pause status if a parameter is specified.")]
        public Task PauseAsync(bool? val = null)
        {
            jukebox.SetPause(Context.Guild, val ?? true);
            return Task.CompletedTask;
        }

        [Command("Skip"), Summary("Skips current song.")]
        public Task SkipAsync()
        {
            jukebox.Skip(Context.Guild);
            return Task.CompletedTask;
        }

        [Command("Clear"), Summary("Clears queue.")]
        public async Task ClearQueue()
        {
            await jukebox.ClearQueueAsync(Context.Guild);
            await ReplyAsync("Cleared queue.");
        }

        [Command("Remove"), Summary("Removes song from queue by index.")]
        public async Task RemoveSongFromQueue(int index)
        {
            var removed = await jukebox.RemoveFromQueueAsync(Context.Guild, index - 1);
            await ReplyAsync($"Removed {removed.Name} from queue.");
        }

        [Command("Queue"), Summary("Shows current queue.")]
        public async Task QueueAsync()
        {
            Models.PlayableMedia[] queue = null;
            try { queue = await jukebox.GetQueueAsync(Context.Guild); }
            catch { }
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

            queue.ForEach((i, x) => eb.AddField(i == 1 ? "Next:" : i.ToString(), i == 1 ? $"**{x.Name}**" : x.Name, false));

            await Context.Channel.SendMessageAsync(null, false, eb.Build());
        }

        [Command("Play"), Alias("P"), Summary("Plays the specified song.")]
        public async Task PlayAsync([Remainder] string songQuery)
        {
            if(GetUserVoiceChannel() == null)
            {
                await ReplyAsync("You need to be in a voice channel, or specify one as a command parameter.");
                return;
            }

            var loop = songQuery.EndsWith(" !loop");
            if (loop)
                songQuery = songQuery.Replace(" !loop", null);

            await jukebox.PlayAsync(Context.Guild, GetUserVoiceChannel(), songQuery, async (context) =>
            {
                await ReplyAsync($"{(context.putInQueue ? "**Queued**" : "**Now Playing**")} {context.song}");
                if (loop)
                    jukebox.SetLooping(Context.Guild, true);
            });
        }

        [Command("Stop"), Summary("Stops playback.")]
        public async Task StopAsync()
        {
            await jukebox.StopAsync(Context.Guild);
            await ReplyAsync("Stopped playback.");
        }
    }
}
