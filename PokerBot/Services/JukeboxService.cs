using System;
using System.Collections.Generic;
using System.Text;
using Discord;
using Discord.WebSocket;
using Discord.Audio;
using System.Threading.Tasks;
using System.Diagnostics;

namespace PokerBot.Services
{
    public sealed class JukeboxService : IDisposable
    {
        public static IVoiceChannel CurrentChannel { get; private set; }
        private static IAudioClient currentClient;

        private static Process ffmpeg;

        private Task<Process> CreatePlayerAsync(string file) =>
             Task.FromResult(new Process()
             {
                 StartInfo = new ProcessStartInfo()
                 {
                     FileName = "ffmpeg.exe",
                     Arguments = $"-i \"mediacache/{file}.mp3\" -ac 2 -f s16le -ar 48000 -loglevel panic -hide_banner pipe:1",
                     UseShellExecute = false,
                     RedirectStandardError = true,
                     RedirectStandardOutput = true,
                     CreateNoWindow = false,
                 },
                 EnableRaisingEvents = true
             });

        public async Task JoinChannelAsync(IVoiceChannel channel)
        {
            if (CurrentChannel == channel)
                throw new Exception("Already in channel.");

            CurrentChannel = channel;
            currentClient = await CurrentChannel.ConnectAsync();
        }

        public async Task LeaveChannelAsync()
        {
            if (CurrentChannel == null)
                throw new Exception("Not in any voice channel.");

            await CurrentChannel.DisconnectAsync();
            CurrentChannel = null;

            currentClient.Dispose();
            currentClient = null;
        }

        public async Task PlayAsync(string songName)
        {
            if (CurrentChannel == null)
                throw new Exception("Not in a voice channel.");

            ffmpeg = await CreatePlayerAsync(songName);
            ffmpeg.Start();

            using var discord = currentClient.CreatePCMStream(AudioApplication.Music);
            using var output = ffmpeg.StandardOutput.BaseStream;
            try { await output.CopyToAsync(discord); }
            catch (TaskCanceledException) { }

            await discord.FlushAsync();
            ffmpeg.Dispose();
        }

        public Task StopAsync() //TODO: Find a better way to stop playback. 
        {
            Dispose();
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            ffmpeg.Dispose();
            currentClient.Dispose();
        }
    }
}
