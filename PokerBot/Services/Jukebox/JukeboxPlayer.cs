using Discord;
using Discord.Audio;
using PokerBot.Entities;
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

        private readonly ConcurrentQueue<(string songName, string songPath)> songQueue =
            new ConcurrentQueue<(string songName, string songPath)>();

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

        public (string songName, string songPath)[] GetQueue() => songQueue.ToArray();

        public void Queue(string songName, string songPath)
        {
            if (!File.Exists(songPath))
                throw new Exception("Specified song path to queue is empty.");
            songQueue.Enqueue((songName, songPath));
        }

        public (string songName, string songPath) DequeueNext()
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

        public async Task PlayAsync(string songPath, string songName, int bitrate = Jukebox.DefaultBitrate, int bufferSize = Jukebox.DefaultBufferSize)
        {
            audio?.Stop();
            audio = null;
            using var playerOut = (audio = new AudioProcessor(songPath, bitrate, bufferSize)).GetOutput();
            discordOut ??= (audioClient ??= await channel.ConnectAsync()).CreatePCMStream(AudioApplication.Music, Bitrate, 1000, 0);

            CurrentSong = songName;

            playing = true;
            await WriteToChannelAsync(playerOut);
            playing = false;

            if (!skip && Looping)
            {
                await PlayAsync(songPath, songName).ConfigureAwait(false);
                return;
            }

            if (!songQueue.IsEmpty)
            {
                var next = DequeueNext();
                await PlayAsync(next.songPath, next.songName).ConfigureAwait(false);
                return;
            }
            CurrentSong = null;
            audio.Stop();
            audio = null;
        }

        public void Dispose()
        {
            SetStopped(true);
            audio?.Stop();
            audio = null;
            channel?.DisconnectAsync();
            channel = null;
            audioClient?.Dispose();
            audioClient = null;
        }
    }
}
