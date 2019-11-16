using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Discord.Audio;
using PokerBot.Services;
using System.Threading.Tasks;

namespace PokerBot.Commands
{
    [Group("Jukebox"), Alias("Audio", "Player")]
    public class JukeboxCommandModule : ModuleBase<SocketCommandContext>
    {
        public JukeboxCommandModule(StandardJukebox jukebox)
        {
            this.jukebox = jukebox;
        }

        private readonly StandardJukebox jukebox;

        [Command("Loop"), Summary("Loops the current song.")]
        public async Task SetLoopingAsync(bool val) =>
            await jukebox.SetLoopingAsync(val);

        [Command("Song"), Summary("Gets the currently playing song.")]
        public async Task GetSongAsync() =>
            await ReplyAsync(await jukebox.GetCurrentSongAsync() ?? "No song playing.");

        [Command("Join"), Summary("Joins the specified voice channel, or the channel the calling user is in.")]
        public async Task JoinAsync(string channelName = null) =>
            await jukebox.JoinChannelAsync(channelName == null ? (Context.User as SocketGuildUser).VoiceChannel : Context.Guild.VoiceChannels.Single(x => x.Name == channelName));

        [Command("Leave"), Summary("Leaves the voice channel.")]
        public async Task LeaveAsync() =>
            await jukebox.LeaveChannelAsync();

        [Command("Resume"), Summary("Resumes playback.")]
        public async Task ResumeAsync() =>
            await jukebox.SetPauseAsync(false);

        [Command("Pause"), Summary("Pauses playback.")]
        public async Task PauseAsync() =>
            await jukebox.SetPauseAsync(true);

        [Command("Queue"), Summary("Queues a song.")]
        public async Task QueueAsync([Remainder] string song = null)
        {
            if (song == null)
            {
                var queue = (await jukebox.GetQueueAsync()).ToArray();

                if(queue.Length == 0)
                {
                    await ReplyAsync("No songs queued.");
                    return;
                }

                var fields = new List<EmbedFieldBuilder>();
                foreach (var item in queue)
                    fields.Add(new EmbedFieldBuilder() { IsInline = false, Name = item });

                EmbedBuilder eb = new EmbedBuilder()
                {
                    Title = "Current Queue",
                    Fields = fields,
                    Color = Color.Blue,
                    Timestamp = DateTimeOffset.Now
                };
                await ReplyAsync(null, false, eb.Build());
                return;
            }

            await jukebox.QueueAsync(song);
        }

        [Command("Play"), Summary("Plays the specified song.")]
        public async Task PlayAsync([Remainder] string song)
        {
            if (!jukebox.IsInChannel())
                await JoinAsync();

            await jukebox.PlayAsync(song, async song => await ReplyAsync($"**Now playing:** {song}"));
        }

        [Command("Stop"), Summary("Stops playback.")]
        public async Task StopAsync()
        {
            await jukebox.StopAsync();
            await ReplyAsync("Stopped playback.");
        }
    }
}
