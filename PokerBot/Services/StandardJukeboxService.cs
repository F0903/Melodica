using System;
using System.Collections.Generic;
using System.Text;
using Discord;
using Discord.WebSocket;
using Discord.Audio;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Threading;
using System.IO;

namespace PokerBot.Services
{
    public class StandardJukeboxService : IAsyncJukeboxService
    {
        private static IVoiceChannel currentVoice;
        private static IAudioClient voiceClient;

        private static AudioOutStream discordOut;

        private static bool paused = false;
        private static bool playing = false;

        private Task<Process> CreatePlayerAsync(string file)
        {
            if (!File.Exists(file))
                throw new FileNotFoundException("The mp3 file was not found.", $"{Path.GetFileName(file)}");

            return Task.FromResult(new Process()
            {
                StartInfo = new ProcessStartInfo()
                {
                    FileName = "ffmpeg.exe",
                    Arguments = $"-i \"{file}\" -filter:a loudnorm -ac 2 -f s16le -ar 48000 -loglevel panic -hide_banner pipe:1",
                    UseShellExecute = false,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    CreateNoWindow = false,
                },
                EnableRaisingEvents = true
            });
        }

        public bool IsInChannel() => voiceClient != null;

        public Task PauseAsync()
        {
            paused = true;
            return Task.CompletedTask;
        }

        public Task ResumeAsync()
        {
            paused = false;
            return Task.CompletedTask;
        }

        public Task TogglePlaybackAsync()
        {
            paused = !paused;
            return Task.CompletedTask;
        }

        public async Task JoinChannelAsync(IVoiceChannel channel)
        {
            if (currentVoice == channel)
                throw new Exception("Already in channel.");

            currentVoice = channel;
            voiceClient = await currentVoice.ConnectAsync();
        }

        public async Task LeaveChannelAsync()
        {
            if (voiceClient == null)
                throw new Exception("Not connected to any channel.");

            await currentVoice.DisconnectAsync();
            voiceClient.Dispose();
            currentVoice = null;
            voiceClient = null;
        }

        public async Task PlayAsync(string songName)
        {
            if (voiceClient == null)
                throw new Exception("Not in a voice channel.");

            if (playing)
                throw new Exception("Already playing.");

            using var ffmpeg = await CreatePlayerAsync($"mediacache/{songName}.mp3");
            ffmpeg.Start();

            using var output = ffmpeg.StandardOutput.BaseStream;
            discordOut ??= voiceClient.CreatePCMStream(AudioApplication.Music, 125 * 1024, 10, 0);
            playing = true;

            byte[] buffer = new byte[1024];

            int count;
            while ((count = await output.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                if (!playing)
                    break;

                while (paused) { }

                await discordOut.WriteAsync(buffer, 0, count);
            }
            await output.FlushAsync();
            await discordOut.FlushAsync();
        }       

        public Task StopAsync()
        {
            playing = false;
            return Task.CompletedTask;
        }
    }
}
