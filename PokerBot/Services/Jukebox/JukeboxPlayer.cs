using Discord;
using Discord.Audio;
using PokerBot.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
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

        private readonly ConcurrentQueue<PlayableMedia> songQueue =
            new ConcurrentQueue<PlayableMedia>();

        private IAudioChannel channel;

        private IAudioClient audioClient;

        private bool skip = false;

        private bool playing = false;

        private AudioOutStream discordOut;

        private AudioProcessor audio;

        private CancellationTokenSource playCancel;

        private async Task WriteToChannelAsync(Stream input)
        {
            playCancel = new CancellationTokenSource();
            byte[] buffer = new byte[BufferSize];
            int bytesRead;
            try
            {
                while ((bytesRead = await input.ReadAsync(buffer, 0, buffer.Length, playCancel.Token).ConfigureAwait(false)) != 0)
                {
                    while (Paused)
                    {
                        if (skip)
                            break;
                        await Task.Delay(1000);
                    }

                    if (skip)
                        break;
                    await discordOut.WriteAsync(buffer, 0, bytesRead, playCancel.Token).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) { } // Catch exception when stopping playback
            skip = false;
        }

        public bool IsPlaying() => playing;

        public string GetChannelName() => channel.Name;

        public bool IsInChannel() => GetChannelName() != null;

        public void Skip() => skip = true;

        public PlayableMedia[] GetQueue() => songQueue.ToArray();

        public void Stop()
        {
            playCancel?.Cancel(false);
            playCancel?.Dispose();
            songQueue.Clear();
        }

        public void Queue(PlayableMedia media, Action<(string song, bool queued)> callback = null)
        {
            if (!File.Exists(media.Path))
                throw new Exception("Specified song path to queue is empty.");
            songQueue.Enqueue(media);
            callback?.Invoke((media.Name, true));
        }

        public PlayableMedia DequeueNext()
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

        public async Task PlayAsync(MediaCollection col, Action<(string song, bool queued)> callback = null, int bitrate = Jukebox.DefaultBitrate, int bufferSize = Jukebox.DefaultBufferSize)
        {
            if (col.IsPlaylist)
            {
                for (int i = 1; i < col.Length; i++)
                    Queue(col[i]);
                callback?.Invoke((col.PlaylistName, true));
            }

            if (playing)
            {
                Queue(col[0], callback);
                return;
            }

            var song = col[0];

            using var playerOut = (audio = new AudioProcessor(song.Path, bitrate, bufferSize / 2, song.Format)).GetOutput();
            discordOut ??= (audioClient ??= await channel.ConnectAsync()).CreatePCMStream(AudioApplication.Music, Bitrate, 100, 0);

            CurrentSong = song.Name;

            callback?.Invoke((song.Name, false));

            playing = true;
            await WriteToChannelAsync(playerOut);
            playing = false;

            CurrentSong = null;
            audio.Dispose();

            if (playCancel.IsCancellationRequested)
                return;

            if (!skip && Looping)
            {
                await PlayAsync(new MediaCollection(song), callback).ConfigureAwait(false);
                return;
            }

            if (!songQueue.IsEmpty)
            {
                var next = DequeueNext();
                await PlayAsync(new MediaCollection(next), callback).ConfigureAwait(false);
                return;
            }
        }

        public void Dispose()
        {
            Stop();
            audio?.Dispose();
            audio = null;
            channel?.DisconnectAsync();
            channel = null;
            audioClient?.Dispose();
            audioClient = null;
        }
    }
}
