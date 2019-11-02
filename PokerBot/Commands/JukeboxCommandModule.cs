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
        public JukeboxCommandModule(StandardJukeboxService jukebox)
        {
            this.jukebox = jukebox;
        }

        private readonly StandardJukeboxService jukebox;

        [Command("Join"), Summary("Joins the specified voice channel, or the channel the calling user is in.")]
        public async Task JoinAsync(string channelName = null) =>
            await jukebox.JoinChannelAsync(channelName == null ? (Context.User as SocketGuildUser).VoiceChannel : Context.Guild.VoiceChannels.Single(x => x.Name == channelName));

        [Command("Leave"), Summary("Leaves the voice channel.")]
        public async Task LeaveAsync() =>
            await jukebox.LeaveChannelAsync();

        [Command("Toggle"), Summary("Toggles playback.")]
        public async Task ToggleAsync() =>
            await jukebox.TogglePlaybackAsync();

        [Command("Resume"), Summary("Resumes playback.")]
        public async Task ResumeAsync() =>
            await jukebox.ResumeAsync();

        [Command("Pause"), Summary("Pauses playback.")]
        public async Task PauseAsync() =>
            await jukebox.PauseAsync();

        [Command("Play"), Summary("Plays the specified song.")]
        public async Task PlayAsync([Remainder] string song)
        {
            if (!jukebox.IsInChannel())
                await JoinAsync();

            await jukebox.PlayAsync(song);
        }

        [Command("Stop"), Summary("Stops playback.")]
        public async Task StopAsync()
        {
            await jukebox.StopAsync();
        }
    }
}
