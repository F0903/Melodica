﻿using System.Buffers;
using Discord;
using Discord.Audio;
using Discord.Audio.Streams;
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
    public enum PlayResult
    {
        Occupied,
        Queued,
        Done,
    }

    public bool Paused { get; private set; }

    public bool Playing => !playLock.IsSet;

    public bool Loop => Queue.Loop;

    public bool Shuffle => Queue.Shuffle;

    public bool Repeat => Queue.Repeat;

    public TimeSpan Elapsed => new(durationTimer.Elapsed.Hours, durationTimer.Elapsed.Minutes, durationTimer.Elapsed.Seconds);

    Action<bool>? OnPauseChanged;
    Action? OnSkipRequested;

    private readonly PlaybackStopwatch durationTimer = new();
    private readonly ManualResetEventSlim playLock = new(true);
    private CancellationTokenSource? cancellation;

    private IAudioClient? audioClient;
    private IAudioChannel? audioChannel;
    private bool skipRequested = false;
    private bool downloading = false;

    private JukeboxInterface? currentPlayer;

    private static readonly MemoryPool<byte> memory = MemoryPool<byte>.Shared;

    private FFmpegProcessor audioProcessor = new();

    public MediaQueue Queue { get; } = new();
    public PlayableMedia? Song { get; private set; }

    public async Task SetPausedAsync(bool value)
    {
        if (!Playing) return;
        Paused = value;
        OnPauseChanged?.Invoke(value);
        if (currentPlayer is not null)
        {
            await currentPlayer.SetButtonStateAsync(JukeboxInterfaceButton.PlayPause, !value);
        }
    }

    public async Task SetLoopAsync(bool value)
    {
        Queue.Loop = value;
        if (currentPlayer is not null)
        {
            await currentPlayer.SetButtonStateAsync(JukeboxInterfaceButton.Loop, value);
        }
    }

    public async Task SetShuffleAsync(bool value)
    {
        Queue.Shuffle = value;
        if (currentPlayer is not null)
        {
            await currentPlayer.SetButtonStateAsync(JukeboxInterfaceButton.Shuffle, value);
        }
    }

    public async Task SetRepeatAsync(bool value)
    {
        Queue.Repeat = value;
        if (currentPlayer is not null)
        {
            await currentPlayer.SetButtonStateAsync(JukeboxInterfaceButton.Repeat, value);
        }
    }

    Task ResetState()
    {
        return Task.WhenAll(
            SetPausedAsync(false),
            SetLoopAsync(false),
            SetRepeatAsync(false),
            SetShuffleAsync(false)
        );
    }

    async Task WriteData(PlayableMedia media, OpusEncodeStream output, CancellationToken token)
    {
        const int frameBytes = 3840;
        using var memHandle = memory.Rent(frameBytes);
        var buffer = memHandle.Memory;
        durationTimer.Start();
        try
        {
            OnPauseChanged = status => audioProcessor.SetPause(status);
            OnSkipRequested = () => audioProcessor.StopRequested();
            await audioProcessor.ProcessStreamAsync(media, output, async () =>
            {
                await output.WriteSilentFramesAsync();
                await output.FlushAsync();
            }, token, media.ExplicitDataFormat);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            Log.Error(ex, $"Got exception when trying to write audio:\n{ex.Message}");
        }
        finally
        {
            await output.WriteSilentFramesAsync();
            await output.FlushAsync(token);
            OnPauseChanged = null;
        }
    }

    private async Task SendDataAsync(PlayableMedia media, OpusEncodeStream output, CancellationToken token)
    {
        async Task StartWrite()
        {
            try
            {
                await WriteData(media, output, token);
            }
            finally
            {
                durationTimer.Reset();
            }
        }

        await StartWrite();

        if (skipRequested) skipRequested = false;
    }

    async Task DisconnectAsync()
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

    public async Task StopAsync()
    {
        if (cancellation is null || cancellation.IsCancellationRequested) return;

        await ClearAsync();
        cancellation.Cancel();
    }

    public async Task SkipAsync()
    {
        if (Queue.IsEmpty)
            return;
        await SetLoopAsync(false);
        skipRequested = true;
    }

    public ValueTask ClearAsync() => Queue.ClearAsync();

    async Task OnClientDisconnect(ulong id)
    {
        var users = audioChannel switch
        {
            SocketVoiceChannel svc => svc.ConnectedUsers, // Actually accurate.
            _ => (IReadOnlyCollection<IUser>)await audioChannel!.GetUsersAsync().FlattenAsync() // Will probably report wrong due to caching.
        };
        var currentUsers = users.Where(x => x.Id != id);
        if (!currentUsers.IsOverSize(1))
        {
            await StopAsync();
        }
    }

    async Task ConnectAsync(IAudioChannel audioChannel)
    {
        if (audioClient is not null && (audioClient.ConnectionState == ConnectionState.Connected || audioClient.ConnectionState == ConnectionState.Connecting))
            return;

        audioClient = await audioChannel.ConnectAsync(true);
        this.audioChannel = audioChannel;

        // Setup auto-disconnect when empty.
        audioClient.ClientDisconnected += OnClientDisconnect;
    }

    async Task PlayNextAsync(IAudioChannel channel, OpusEncodeStream output, CancellationToken token)
    {
        var media = await Queue.DequeueAsync() ?? throw new CriticalException("Song from queue was null.");
        Song = media;

        await currentPlayer!.SetSongEmbedAsync(media.Info, null); //TODO: prob reimplement collection info in some form.

        var donePlaying = false;
        try
        {
            await SendDataAsync(media, output, token);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            await DisconnectAsync();
            throw new CriticalException($"SendDataAsync encountered a fatal error. (please report)\n```{ex.Message}```");
        }
        finally
        {
            if (!Loop)
            {
                if (Queue.IsEmpty || cancellation!.IsCancellationRequested)
                {
                    await DisconnectAsync();
                    donePlaying = true;
                }
            }
        }
        if (donePlaying)
            return;

        await PlayNextAsync(channel, output, token);
    }

    public async Task<PlayResult> PlayAsync(IMediaRequest request, IAudioChannel channel, JukeboxInterface player)
    {
        if (downloading)
            return PlayResult.Occupied;

        MediaInfo reqInfo;
        PlayableMedia media;
        try
        {
            downloading = true;
            reqInfo = await request.GetInfoAsync();
            media = await request.GetMediaAsync();
            downloading = false;
        }
        catch(Exception ex)
        {
            downloading = false;
            throw;
        }

        await Queue.EnqueueAsync(media);

        if (Playing)
        {
            return PlayResult.Queued;
        }

        playLock.Wait();
        playLock.Reset();
        try
        {
            cancellation = new();
            var token = cancellation.Token;

            await ConnectAsync(channel); //TODO: will timeout if doesn't have proper permissions in channel, check first.
            var bitrate = (channel as IVoiceChannel)?.Bitrate ?? 96000;
            await player.SpawnAsync(reqInfo, null);
            currentPlayer = player;
            using var output = (OpusEncodeStream)audioClient!.CreatePCMStream(AudioApplication.Music, bitrate, 1000, 0);
            await PlayNextAsync(channel, output, token);
        }
        finally
        {
            playLock.Set();
            await player.DisableAllButtonsAsync();
            await ResetState();
            if (audioClient is not null)
                audioClient.ClientDisconnected -= OnClientDisconnect;
        }
        return PlayResult.Done;
    }

    public async Task SwitchAsync(IMediaRequest request)
    {
        // Must be sequential.
        await SetNextAsync(request);
        await SetLoopAsync(false);
        await SkipAsync();
    }

    public async Task SetNextAsync(IMediaRequest request)
    {
        var media = await request.GetMediaAsync();
        await Queue.PutFirstAsync(media);
    }
}
