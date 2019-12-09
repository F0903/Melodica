using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Discord.Audio;
using PokerBot.Services.Jukebox;
using PokerBot.Utility.Extensions;
using System.Threading.Tasks;

namespace PokerBot.Commands
{
    //[Group("Jukebox"), Alias("J")]
    public class JukeboxCommandModule : ModuleBase<SocketCommandContext>
    {
        private IVoiceChannel GetUserVoiceChannel() => ((SocketGuildUser)Context.User).VoiceChannel;

        [Command("Loop"), Summary("Loops the current song.")]
        public async Task SetLoopingAsync(bool val)
        {
            Jukebox.SetLooping(Context.Guild, val);
            await ReplyAsync($"Loop set to {val}");
        }

        [Command("Song"), Summary("Gets the currently playing song.")]
        public async Task GetSongAsync()
        {
            await ReplyAsync($"**Currently playing** {Jukebox.GetPlayingSong(Context.Guild)}");
        }

        [Command("Join"), Summary("Joins the specified voice channel, or the channel the calling user is in.")]
        public async Task JoinAsync(string channelName = null)
        {
            await Jukebox.JoinChannelAsync(Context.Guild, channelName == null ? GetUserVoiceChannel() : Context.Guild.VoiceChannels.Single(x => x.Name == channelName));
        }

        [Command("Leave"), Summary("Leaves the voice channel.")]
        public async Task LeaveAsync()
        {
            await Jukebox.LeaveChannelAsync(Context.Guild);
        }

        [Command("Resume"), Summary("Resumes playback.")]
        public Task ResumeAsync()
        {
            Jukebox.SetPause(Context.Guild, false);
            return Task.CompletedTask;
        }

        [Command("Pause"), Summary("Pauses playback or sets the pause status if a parameter is specified.")]
        public Task PauseAsync(bool? val = null)
        {
            Jukebox.SetPause(Context.Guild, val ?? true);
            return Task.CompletedTask;
        }

        [Command("Skip"), Summary("Skips current song.")]
        public Task SkipAsync()
        {
            Jukebox.Skip(Context.Guild);
            return Task.CompletedTask;
        }

        [Command("Queue"), Summary("Shows current queue.")]
        public async Task QueueAsync()
        {
            (string song, string path, string format)[] queue = null;
            try { queue = await Jukebox.GetQueueAsync(Context.Guild); }
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

            queue.ForEach((i, x) => eb.AddField(i == 1 ? "**Next:**" : i.ToString(), $"**{x.song}**", false));

            await Context.Channel.SendMessageAsync(null, false, eb.Build());
        }

        [Command("Play"), Alias("P"), Summary("Plays the specified song.")]
        public async Task PlayAsync([Remainder] string songQuery)
        {
            var loop = songQuery.EndsWith(" !loop");
            if (loop)
                songQuery = songQuery.Replace(" !loop", null);

            await Jukebox.PlayAsync(Context.Guild, GetUserVoiceChannel(), songQuery, async (context) =>
            {
                await ReplyAsync($"{(context.putInQueue ? "**Queued**" : "**Now Playing**")} {context.song}");
                if (loop)
                    Jukebox.SetLooping(Context.Guild, true);
            });
        }

        [Command("Stop"), Summary("Stops playback.")]
        public async Task StopAsync()
        {
            await Jukebox.StopAsync(Context.Guild);
            await ReplyAsync("Stopped playback.");
        }
    }
}
