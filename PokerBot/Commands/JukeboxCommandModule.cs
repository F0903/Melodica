﻿using System;
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
        public JukeboxCommandModule(JukeboxService jukebox)
        {
            this.jukebox = jukebox;
        }

        private readonly JukeboxService jukebox;

        [Command("Join"), Summary("Joins the specified voice channel, or the channel the calling user is in.")]
        public async Task JoinAsync(string channelName = null) =>
            await jukebox.JoinChannelAsync(channelName == null ? (Context.User as SocketGuildUser).VoiceChannel : Context.Guild.VoiceChannels.Single(x => x.Name == channelName));

        [Command("Leave"), Summary("Leaves the voice channel.")]
        public async Task LeaveAsync() =>
            await jukebox.LeaveChannelAsync();

        [Command("Play"), Summary("Plays the specified song.")]
        public async Task PlayAsync(string song)
        {
            await jukebox.PlayAsync(song);
        }

        [Command("Stop"), Summary("Stops the music.")]
        public async Task StopAsync()
        {
            await jukebox.StopAsync();
        }
    }
}