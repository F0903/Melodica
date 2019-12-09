using Discord;
using Discord.Audio;
using PokerBot.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace PokerBot.Services.Jukebox
{
    public sealed class JukeboxPlayer : IDisposable
    {
        public JukeboxPlayer(int bitrate = Jukebox.DefaultBitrate, int bufferSize = Jukebox.DefaultBufferSize, IAudioChannel channel = null)
        {
            Bitrate = bitrate;
            BufferSize = bufferSize;
            this.channel = channel;
        }

        public int Bitrate { get; private set; }

        public int BufferSize { get; private set; }

        public string CurrentSong { get; private set; }

        public bool Paused { get; set; } = false;

        public bool Looping { get; set; } = false;

        private readonly ConcurrentQueue<(string songName, string songPath, string songFormat)> songQueue =
            new ConcurrentQueue<(string songName, string songPath, string songFormat)>();

        private IAudioChannel channel;

        private IAudioClient audioClient;

        private bool stopped = false;

        private bool skip = false;

        private bool playing = false;

        private AudioOutStream discordOut;

        private AudioProcessor audio;

        private async Task WriteToChannelAsync(Stream input)
        {
            byte[] buffer = new byte[BufferSize];
            int bytesRead;
            while ((bytesRead = await input.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false)) != 0)
            {
                while (Paused)
                {
                    if (stopped || skip)
                        break;
                    await Task.Delay(1000);
                }

                if (skip)
                    break;

                if (stopped)
                {
                    await discordOut.FlushAsync();
                    songQueue.Clear();
                    return;
                }

                await discordOut.WriteAsync(buffer, 0, bytesRead).ConfigureAwait(false);
            }
            skip = false;
        }

        public bool IsPlaying() => playing;

        public string GetChannelName() => channel.Name;

        public bool IsInChannel() => GetChannelName() != null;

        public bool SetStopped(bool val) => stopped = val;

        public void Skip() => skip = true;

        public (string songName, string songPath, string songFormat)[] GetQueue() => songQueue.ToArray();

        public void Queue(DownloadResult res, Action<(string song, bool queued)> callback)
        {
            var result = res.GetResult();
            if (res.isPlaylist)
            {
                foreach (var (name, path, format) in result)
                {
                    if (!File.Exists(path))
                        throw new Exception("Specified song path to queue is empty.");
                    songQueue.Enqueue((name, path, format));
                    callback?.Invoke((name, true));
                }
                return;
            }

            var video = result[0];
            if (!File.Exists(video.path))
                throw new Exception("Specified song path to queue is empty.");
            songQueue.Enqueue((video.name, video.path, video.format));
            callback?.Invoke((video.name, true));
        }

        public (string songName, string songPath, string songFormat) DequeueNext()
        {
            songQueue.TryDequeue(out var res);
            return res;
        }

        public void ClearQueue()
        {
            songQueue.Clear();
        }

        public async Task ConnectToChannelAsync(IAudioChannel channel)
        {
            this.channel = channel;
            audioClient = await channel.ConnectAsync();
        }

        public async Task LeaveChannelAsync()
        {
            await channel.DisconnectAsync();
        }

        public async Task PlayAsync(DownloadResult res, Action<(string song, bool queued)> callback, int bitrate = Jukebox.DefaultBitrate, int bufferSize = Jukebox.DefaultBufferSize)
        {
            var result = res.GetResult();

            if (playing || result.Length > 1)
                Queue(res, callback);

            if (playing)
                return;

            var vid = result[0];

            using var playerOut = (audio = new AudioProcessor(vid.path, bitrate, bufferSize, vid.format)).GetOutput();
            discordOut ??= (audioClient ??= await channel.ConnectAsync()).CreatePCMStream(AudioApplication.Music, Bitrate, 100, 0);

            CurrentSong = vid.name;

            callback?.Invoke((vid.name, false));
            playing = true;
            await WriteToChannelAsync(playerOut);
            playing = false;

            if (!skip && Looping)
            {
                await PlayAsync(new DownloadResult(vid.name, vid.path, vid.format), callback).ConfigureAwait(false);
                return;
            }

            if (!songQueue.IsEmpty)
            {
                var next = DequeueNext();
                await PlayAsync(new DownloadResult(next.songName, next.songPath, next.songFormat), callback).ConfigureAwait(false);
                return;
            }
            CurrentSong = null;
            audio.Dispose();
        }

        public void Dispose()
        {
            SetStopped(true);
            audio?.Dispose();
            audio = null;
            channel?.DisconnectAsync();
            channel = null;
            audioClient?.Dispose();
            audioClient = null;
        }
    }
}
