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

        private IVoiceChannel GetUserVoiceChannel() => ((SocketGuildUser)Context.User).VoiceChannel;
        
        [Command("Loop"), Summary("Loops the current song.")]
        public Task SetLoopingAsync(bool val)
        {
            
            return Task.CompletedTask;
        }

        [Command("Song"), Summary("Gets the currently playing song.")]
        public async Task GetSongAsync()
        {
            await ReplyAsync(jukebox.GetPlayingSong(Context.Guild));
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
            throw new NotImplementedException("This function has not yet been implemented.");
            return Task.CompletedTask;
        }

        [Command("Queue"), Summary("Queues a song.")]
        public async Task QueueAsync([Remainder] string song = null)
        {
            throw new NotImplementedException("This function has not yet been implemented.");
        }

        [Command("Play"), Summary("Plays the specified song.")]
        public async Task PlayAsync([Remainder] string songQuery)
        {
            await jukebox.PlayAsync(Context.Guild, GetUserVoiceChannel(), songQuery, song => ReplyAsync($"**Now Playing:** {song}"));
        }

        [Command("Stop"), Summary("Stops playback.")]
        public async Task StopAsync()
        {
            await jukebox.StopAsync(Context.Guild);
            await ReplyAsync("Stopped playback.");
        }
    }
}
