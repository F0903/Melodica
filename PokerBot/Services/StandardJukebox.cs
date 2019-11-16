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
using System.Collections.Concurrent;

namespace PokerBot.Services
{
    public class StandardJukebox : IAsyncJukeboxService
    {
        public StandardJukebox(AsyncYoutubeDownloader downloaderService, AsyncFileCache cacheService)
        {
            downloader = downloaderService;
            cache = cacheService;
        }

        public const int BitRate = 128 * 1024;

        public const int BufferSize = 64 * 1024;

        public const int QueueLimit = 100;

        private readonly AsyncFileCache cache;

        private readonly AsyncYoutubeDownloader downloader;

        private readonly ConcurrentQueue<string> queue = new ConcurrentQueue<string>();

        private static volatile IVoiceChannel currentVoice;
        private static volatile IAudioClient voiceClient;

        private static volatile AudioOutStream discordOut;

        private static volatile bool paused = false;
        private static volatile bool playing = false;
        private static volatile bool looping = false;

        private static string currentSong;

        public Task<string> GetCurrentSongAsync() => Task.FromResult(currentSong);

        public Task<string[]> GetQueueAsync() => Task.FromResult(queue.ToArray());

        public bool IsInChannel() => voiceClient != null;

        private Task<Process> CreatePlayerAsync(string file = null) // If file is null, use stdin
        {
            var player = new Process()
            {
                StartInfo = new ProcessStartInfo()
                {
                    FileName = "ffmpeg.exe",
                    Arguments = $"-loglevel debug -hide_banner -f webm -i {file ?? "pipe:0"} -af loudnorm=I=-16:TP=-1.5:LRA=11 -ac 2 -f s16le -ar 48000 -b:a {BitRate} -y -bufsize {BufferSize} pipe:1",
                    UseShellExecute = false,
                    RedirectStandardError = false,
                    RedirectStandardOutput = true,
                    RedirectStandardInput = true,
                    CreateNoWindow = false,
                }
            };

            return Task.FromResult(player);
        }

        private async Task<(string originalName, string path)> GetSongAsync(string query)
        {
            if (downloader is AsyncYoutubeDownloader)
            {
                var title = await downloader.GetVideoTitleAsync(query);
                if (cache.ExistsInCache(title))
                    return (title, await cache.GetValueAsync(title));
            }

            var (stream, name) = await downloader.DownloadAsync(query);
            return (name, await cache.CacheAsync((stream, name)));
        }             

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

            queue.Enqueue((await GetSongAsync(songName)).originalName);
        }

        public async Task PlayAsync(string songName, Func<string, Task> startPlayCallbackAsync = null)
        {
            if (voiceClient == null)
                throw new Exception("Not in a voice channel.");

            if (playing)
                await StopAsync();

            (string songToPlay, string songPath) = await GetSongAsync(songName);

            using var ffmpeg = await CreatePlayerAsync(songPath);
            ffmpeg.Start();

            // Commented lines to use for RamCache (probably just delete it)

            //using var input = ffmpeg.StandardInput.BaseStream;
            //await input.WriteAsync(await cache.GetCacheAsync(songToPlay));
            //await input.DisposeAsync();

            using var output = ffmpeg.StandardOutput.BaseStream;

            discordOut ??= voiceClient.CreatePCMStream(AudioApplication.Music, BitRate, 1000, 0);

            playing = true;
            currentSong = songToPlay;

            startPlayCallbackAsync?.Invoke(songToPlay);

            byte[] buffer = new byte[BufferSize];
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
            await discordOut.FlushAsync();
            ffmpeg.Close();

            if (voiceClient == null)
                return;

            if (looping)
            {
                await PlayAsync(songName, startPlayCallbackAsync);
                return;
            }

            if (queue.Count > 0)
            {
                if (!queue.TryDequeue(out var res))
                    return;
                await PlayAsync(res, startPlayCallbackAsync);
            }

            await StopAsync();
        }

        public Task StopAsync()
        {
            currentSong = null;
            playing = false;
            looping = false;
            return Task.CompletedTask;
        }
    }
}
