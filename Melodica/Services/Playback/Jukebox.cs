﻿using Discord;
using Discord.Audio;
using Discord.WebSocket;

using Melodica.Core.Exceptions;
using Melodica.Services.Audio;
using Melodica.Services.Media;
using Melodica.Services.Playback.Requests;
using Melodica.Utility;

using Serilog;

namespace Melodica.Services.Playback;

public sealed class Jukebox
{
    public Jukebox(IMessageChannel callbackChannel)
    {
        this.callbackChannel = callbackChannel;
    }

    public enum PlayResult
    {
        Occupied,
        Queued,
        Done,
    }

    public bool Paused { get; private set; }

    public bool Playing => !playLock.IsSet;

    public bool Loop => queue.Loop;

    public bool Shuffle => queue.Shuffle;

    public bool Repeat => queue.Repeat;

    public TimeSpan Elapsed => new(durationTimer.Elapsed.Hours, durationTimer.Elapsed.Minutes, durationTimer.Elapsed.Seconds);

    private readonly PlaybackStopwatch durationTimer = new();
    private readonly MediaQueue queue = new();
    private readonly ManualResetEventSlim playLock = new(true);
    private CancellationTokenSource? cancellation;

    private IAudioClient? audioClient;
    private IAudioChannel? audioChannel;
    private readonly IMessageChannel callbackChannel;

    private bool skipRequested = false;
    private bool downloading = false;

    private Player? currentPlayer;
    private PlayableMedia? currentSong;

    public async Task SetPaused(bool value)
    {
        if (!Playing) return;
        Paused = value;
        if (currentPlayer is not null)
        {
            await currentPlayer.SetButtonStateAsync(PlayerButton.PlayPause, !value);
        }
    }

    public async Task SetLoop(bool value)
    {
        queue.Loop = value;
        if (currentPlayer is not null)
        {
            await currentPlayer.SetButtonStateAsync(PlayerButton.Loop, value);
        }
    }

    public async Task SetShuffle(bool value)
    {
        queue.Shuffle = value;
        if (currentPlayer is not null)
        {
            await currentPlayer.SetButtonStateAsync(PlayerButton.Shuffle, value);
        }
    }

    public async Task SetRepeat(bool value)
    {
        queue.Repeat = value;
        if (currentPlayer is not null)
        {
            await currentPlayer.SetButtonStateAsync(PlayerButton.Repeat, value);
        }
    }

    Task ResetState()
    {
        return Task.WhenAll(
            SetPaused(false),
            SetLoop(false),
            SetRepeat(false),
            SetShuffle(false)
            );
    }

    public MediaQueue GetQueue()
    {
        return queue;
    }

    public PlayableMedia? GetSong()
    {
        return currentSong;
    }

    static async ValueTask<bool> CheckIfAloneAsync(IAudioChannel channel)
    {
        var users = await channel.GetUsersAsync().FirstAsync();
        return !users.IsOverSize(1);
    }

    static void SendSilence(AudioOutStream output, int frames = 124)
    {
        const int channels = 2;
        const int bits = 16;
        const int blockAlign = channels * bits;
        int bytes = blockAlign * frames;
        Span<byte> buffer = bytes < 1024 ? stackalloc byte[bytes] : new byte[bytes];
        output.Write(buffer);
        output.Flush();
    }

    void WriteData(Stream input, AudioOutStream output, CancellationToken token)
    {
        bool BreakConditions() => token.IsCancellationRequested || skipRequested;

        void Pause()
        {
            durationTimer.Stop();
            SendSilence(output);
            while (Paused && !BreakConditions())
            {
                Thread.Sleep(1000);
            }
            durationTimer.Start();
        }

        int count;
        Span<byte> buffer = new byte[4 * 1024];
        durationTimer.Start();
        while ((count = input!.Read(buffer)) != 0)
        {
            if (Paused)
            {
                Pause();
            }

            if (BreakConditions())
            {
                if (token.IsCancellationRequested)
                    return;
                SendSilence(output);
                break;
            }

            output.Write(buffer[..count]);
        }
        SendSilence(output);
    }

    private Task<bool> SendDataAsync(DataInfo data, AudioOutStream output, IAudioChannel channel, CancellationToken token)
    {

        bool aborted = false;

        async void StartWrite()
        {
            try
            {
                var mediaPath = data.MediaPath ?? throw new NullReferenceException("MediaPath was not specified. (internal error)");
                using IAsyncAudioProcessor audio = data.Format == "s16le" ? new RawProcessor(mediaPath) : new FFmpegProcessor(mediaPath, data.Format);
                using var input = await audio.ProcessAsync();
                WriteData(input, output, token);
            } 
            catch (Exception ex)
            {
                Log.Error(ex, "SendDataAsync encountered an exception.");
                aborted = true;
            }
            finally
            {
                durationTimer.Reset();
            }
        }

        var writeThread = new Thread(StartWrite)
        {
            IsBackground = false,
            Priority = ThreadPriority.Highest
        };

        writeThread.Start();
        writeThread.Join(); // Thread exit

        if (skipRequested) skipRequested = false;

        return Task.FromResult(aborted); // Returns true if an error occured.
    }

    async ValueTask DisconnectAsync()
    {
        await StopAsync();
        if (audioChannel is not null)
        {
            await audioChannel.DisconnectAsync();
            audioChannel = null;
        }
        if (audioClient is not null)
        {
            await audioClient.StopAsync();
            audioClient.Dispose();
            audioClient = null;
        }
    }

    public async ValueTask StopAsync()
    {
        if (cancellation is null || (cancellation is not null && cancellation.IsCancellationRequested))
            return;

        await ClearAsync();
        cancellation!.Cancel();
        playLock.Wait(5000);
    }

    public void Skip()
    {
        if (queue.IsEmpty)
            return;
        SetLoop(false).Wait();
        skipRequested = true;
    }

    public ValueTask ClearAsync()
    {
        return queue.ClearAsync();
    }

    private async ValueTask ConnectAsync(IAudioChannel audioChannel)
    {
        if (audioClient is not null && (audioClient.ConnectionState == ConnectionState.Connected || audioClient.ConnectionState == ConnectionState.Connecting))
            return;

        audioClient = await audioChannel.ConnectAsync(true);
        this.audioChannel = audioChannel;

        // Setup auto-disconnect when empty.
        audioClient.ClientDisconnected += async (_) =>
        {
            IReadOnlyCollection<IUser> users;
            if (audioChannel is SocketVoiceChannel svc)
            {
                users = svc.ConnectedUsers; // Actually accurate.
            }
            else
            {
                users = (IReadOnlyCollection<IUser>)await audioChannel.GetUsersAsync().FlattenAsync(); // Will probably report wrong due to caching.
            }

            if (!users.IsOverSize(1))
            {
                await StopAsync();
            }
        };
    }

    //TODO: Implement starting point. (seeking)
    async Task PlayNextAsync(IAudioChannel channel, AudioOutStream output, CancellationToken token, TimeSpan? startingPoint = null)
    {
        PlayableMedia media = await queue.DequeueAsync();
        if (media is null)
            throw new CriticalException("Song from queue was null.");

        currentSong = media;

        DataInfo dataInfo;
        try
        {
            dataInfo = await media.GetDataAsync();
        }
        catch
        {
            if (!queue.IsEmpty) await PlayNextAsync(channel, output, token, null);
            else await DisconnectAsync();
            return;
        }

        await currentPlayer!.SetSongEmbedAsync(media.Info, media.CollectionInfo);
        bool faulted = await SendDataAsync(dataInfo, output, channel, token);

        // If media is temporary (3rd party download) then delete the file.
        if ((!Loop || faulted) && media is TempMedia temp)
        {
            temp.DiscardTempMedia();
        }

        if (faulted)
        {
            await ResetState();
            throw new CriticalException("SendDataAsync encountered a fatal error. (please report)");
        }

        if ((!Loop && queue.IsEmpty) || cancellation!.IsCancellationRequested)
        {
            await DisconnectAsync();
            return;
        }

        await PlayNextAsync(channel, output, token, null);
    }

    public async Task<PlayResult> PlayAsync(IMediaRequest request, IAudioChannel channel, Player player, TimeSpan? startingPoint = null)
    {
        if (downloading)
            return PlayResult.Occupied;

        MediaInfo reqInfo;
        MediaCollection collection;
        try
        {
            downloading = true;
            reqInfo = await request.GetInfoAsync();
            collection = await request.GetMediaAsync();
            downloading = false;
        }
        catch
        {
            downloading = false;
            throw;
        }

        await queue.EnqueueAsync(collection);
        if (Playing)
        {
            return PlayResult.Queued;
        }

        playLock.Wait();
        playLock.Reset();
        try
        {
            cancellation = new();
            CancellationToken token = cancellation.Token;

            await ConnectAsync(channel);
            int bitrate = (channel as IVoiceChannel)?.Bitrate ?? 96000;
            await player.SpawnAsync(reqInfo, collection.CollectionInfo);
            currentPlayer = player;
            using AudioOutStream output = audioClient!.CreatePCMStream(AudioApplication.Music, bitrate, 200, 0);
            await PlayNextAsync(channel, output, token, startingPoint);
        }
        finally
        {
            playLock.Set();
            await player.DisableAllButtonsAsync();
            await ResetState();
        }
        return PlayResult.Done;
    }

    public async Task SwitchAsync(IMediaRequest request)
    {
        await SetNextAsync(request);
        await SetLoop(false);
        Skip();
    }

    public async Task SetNextAsync(IMediaRequest request)
    {
        MediaCollection media = await request.GetMediaAsync();
        await queue.PutFirstAsync(media);
    }
}
