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

        private static readonly Queue<string> queue = new Queue<string>(QueueLimit);

        private static volatile IVoiceChannel currentVoice;
        private static volatile IAudioClient voiceClient;

        private static volatile AudioOutStream discordOut;

        private static volatile bool paused = false;
        private static volatile bool playing = false;
        private static volatile bool looping = false;

        private static string currentSong;

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

        private async Task<string> GetMediaAsync(string query)
        {
            var result = await downloader.DownloadAsync(query);
            return await cache.CacheAsync(result);
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

            queue.Enqueue(await GetMediaAsync(songName));
        }

        public async Task PlayAsync(string songName)
        {
            if (voiceClient == null)
                throw new Exception("Not in a voice channel.");

            if (playing)
                await StopAsync();

            using var ffmpeg = await CreatePlayerAsync(await GetMediaAsync(songName));
            ffmpeg.Start();

            using var output = ffmpeg.StandardOutput.BaseStream;
            discordOut ??= voiceClient.CreatePCMStream(AudioApplication.Music, 128 * 1024, 1, 0);

            playing = true;
            currentSong = songName;

            byte[] buffer = new byte[1024];

            int count;
            while ((count = await output.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                if (!playing)
                    return;

                while (paused) { }

                await discordOut.WriteAsync(buffer, 0, count);
            }

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

            await output.FlushAsync();
            await discordOut.FlushAsync();
            playing = false;
        }

        public async Task StopAsync()
        {
            playing = false;
            await discordOut.FlushAsync();
        }
    }
}
