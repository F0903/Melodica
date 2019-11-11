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
using PokerBot.Utility.Extensions;

namespace PokerBot.Services
{
    public class StandardJukebox : IAsyncJukeboxService
    {
        public StandardJukebox(AsyncYoutubeDownloader downloaderService, IAsyncCachingService cacheService)
        {
            downloader = downloaderService;
            cache = cacheService;

            cache.ClearCache();
        }

        public const int QueueLimit = 100;

        private readonly IAsyncCachingService cache;

        private readonly AsyncYoutubeDownloader downloader;

        private readonly Queue<string> queue = new Queue<string>(QueueLimit);

        private static volatile IVoiceChannel currentVoice;
        private static volatile IAudioClient voiceClient;

        private static volatile AudioOutStream discordOut;

        private static volatile bool paused = false;
        private static volatile bool playing = false;
        private static volatile bool looping = false;

        private static string currentSong;

        private Task<Process> CreatePlayerAsync()
        {
            var player = new Process()
            {
                StartInfo = new ProcessStartInfo()
                {
                    FileName = "ffmpeg.exe",
                    Arguments = $"-loglevel debug -hide_banner -f webm -i pipe:0 -af loudnorm=I=-16:TP=-1.5:LRA=11 -ac 2 -f s16le -ar 48000 -y pipe:1", //
                    UseShellExecute = false,
                    RedirectStandardError = false,
                    RedirectStandardOutput = true,
                    RedirectStandardInput = true,
                    CreateNoWindow = false,
                }
            };

            return Task.FromResult(player);
        }

        private async Task<string> DownloadSongAsync(string query)
        {
            var (stream, name) = await downloader.DownloadAsync(query);
            return await cache.CacheAsync((stream, name));
        }

        public Task<string> GetCurrentSongAsync() => Task.FromResult(currentSong);

        public Task<Queue<string>> GetQueueAsync() => Task.FromResult(queue);

        public bool IsInChannel() => voiceClient != null;

        public Task SetLoopingAsync(bool val)
        {
            looping = val;
            return Task.CompletedTask;
        }

        public Task SetPauseAsync(bool val)
        {
            paused = val;
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
            queue.Clear();
        }

        public async Task QueueAsync(string songName)
        {
            if (queue.Count >= QueueLimit)
                throw new Exception($"Queue has reached the max limit of {QueueLimit}");

            queue.Enqueue(await DownloadSongAsync(songName));
        }

        public async Task PlayAsync(string songName)
        {
            if (voiceClient == null)
                throw new Exception("Not in a voice channel.");

            if (playing)
                await StopAsync();

            string songToPlay = await DownloadSongAsync(songName);

            using var ffmpeg = await CreatePlayerAsync();
            ffmpeg.Start();

            using var input = ffmpeg.StandardInput.BaseStream;
            using var output = ffmpeg.StandardOutput.BaseStream;

            await input.WriteAsync(await cache.GetCacheAsync(songToPlay));
            await input.DisposeAsync();
            
            discordOut ??= voiceClient.CreatePCMStream(AudioApplication.Music, 128 * 1024, 1000, 0);

            playing = true;
            currentSong = songToPlay;

            byte[] buffer = new byte[4 * 1024];
            int outputRead;
            while ((outputRead = await output.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                if (!playing)
                {
                    ffmpeg.Close();
                    return;
                }

                while (paused) { }

                await discordOut.WriteAsync(buffer, 0, outputRead);
            }

            ffmpeg.Close();

            if (looping)
            {
                await PlayAsync(songName);
                return;
            }

            if (queue.Count > 0 && voiceClient != null)
            {
                await PlayAsync(queue.Dequeue());
                return;
            }

            await StopAsync();
        }

        public async Task StopAsync()
        {
            currentSong = null;
            playing = false;
            looping = false;
            await discordOut.FlushAsync();
        }
    }
}
