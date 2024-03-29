﻿using System.Buffers;
using Discord;
using Discord.Audio;
using Discord.Audio.Streams;
using Discord.WebSocket;
using Melodica.Core.Exceptions;
using Melodica.Services.Audio;
using Melodica.Services.Caching;
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

    public bool Loop { get; private set; }

    public bool Shuffle => Queue.Shuffle;

    public bool Repeat => Queue.Repeat;

    public TimeSpan Elapsed => new(durationTimer.Elapsed.Hours, durationTimer.Elapsed.Minutes, durationTimer.Elapsed.Seconds);

    private readonly PlaybackStopwatch durationTimer = new();
    private readonly ManualResetEventSlim playLock = new(true);
    private CancellationTokenSource? stopper;

    private IAudioClient? audioClient;
    private IAudioChannel? audioChannel;

    private JukeboxInterface? currentPlayer;

    private static readonly MemoryPool<byte> memory = MemoryPool<byte>.Shared;

    private readonly FFmpegProcessor mediaProcessor = new FFmpegProcessor();

    public MediaQueue Queue { get; } = new();

    public MediaInfo? CurrentSong { get; set; }

    public async Task SetPausedAsync(bool value)
    {
        if (!Playing) return;
        Paused = value;
        mediaProcessor.SetPause(value);
        if (currentPlayer is not null)
        {
            await currentPlayer.SetButtonStateAsync(JukeboxInterfaceButton.PlayPause, !value);
        }
    }

    public async Task SetLoopAsync(bool value)
    {
        Loop = value;
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

    async Task SendDataAsync(PlayableMediaStream media, OpusEncodeStream output, CancellationToken token)
    {
        const int frameBytes = 3840;

        try
        {
            using var memHandle = memory.Rent(frameBytes);
            var buffer = memHandle.Memory;
            durationTimer.Start();

            await mediaProcessor.ProcessMediaAsync(
                media,
                output,
                async () =>
                {
                    durationTimer.Stop();
                    await output.WriteSilentFramesAsync();
                    await output.FlushAsync();
                },
                () =>
                {
                    durationTimer.Start();
                },
                token
            );
        }
        catch (OperationCanceledException) { }

        Log.Debug("Finished sending data... Flushing...");
        await output.WriteSilentFramesAsync();
        await output.FlushAsync(token);
        durationTimer.Reset();
    }

    async Task DisconnectAsync()
    {
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

    public async Task StopAsync(bool completeStop = true)
    {
        if (stopper is null || stopper.IsCancellationRequested) return;

        if (completeStop)
        {
            var node = await Queue.ClearAsync();
            await stopper.CancelAsync();
            playLock.Wait();
            node?.CloseAll();
            return;
        }

        await stopper.CancelAsync();
    }

    public async Task SkipAsync()
    {
        if (Queue.IsEmpty)
            return;
        await SetLoopAsync(false);
        await StopAsync(false);
    }

    async Task OnClientDisconnect(ulong id)
    {
        var users = audioChannel switch
        {
            SocketVoiceChannel svc => svc.ConnectedUsers, // Seems more accurate
            _ => (IReadOnlyCollection<IUser>)await audioChannel!.GetUsersAsync().FlattenAsync()
        };
        var currentUsers = users.Where(x => x.Id != id);
        if (!currentUsers.IsOverSize(1))
        {
            Log.Debug("Users in VC was under 1. Disconnecting...");
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

    async Task PlayNextAsync(IAudioChannel channel, OpusEncodeStream output)
    {
        if (Queue.IsEmpty)
        {
            await DisconnectAsync();
            return;
        }

        var media = await Queue.DequeueAsync();
        CurrentSong = await media.GetInfoAsync();

        await currentPlayer!.SetSongEmbedAsync(CurrentSong, null); //TODO: Consider reimplementing collectionInfo / playlist info again.

        try
        {
            stopper = new();
            var stopToken = stopper.Token;
            Log.Debug("Starting sending data..");
            do
            {
                await SendDataAsync(media, output, stopToken);
                if (Loop && media.CanSeek) media.Seek(0, SeekOrigin.Begin);
            } while (Loop);
        }
        catch (OperationCanceledException)
        {
            Log.Debug("Caught operation cancelled exception in PlayNext.");
        }
        catch (Exception ex)
        {
            Log.Debug($"Caught critical {ex} exception in PlayNext. Disconnecting...");
            await DisconnectAsync();
            throw new CriticalException($"SendDataAsync encountered a fatal error. (please report)\n```{ex.Message}```");
        }
        finally
        {
            stopper?.Dispose();
            stopper = null;
            await media.DisposeAsync();
        }

        await PlayNextAsync(channel, output);
    }

    public async Task<PlayResult> PlayAsync(IMediaRequest request, IAudioChannel channel, JukeboxInterface playerInterface)
    {
        MediaInfo reqInfo;
        PlayableMediaStream media;

        reqInfo = await request.GetInfoAsync();
        media = await request.GetMediaAsync();

        await Queue.EnqueueAsync(media);

        if (Playing)
        {
            return PlayResult.Queued;
        }

        playLock.Wait();
        playLock.Reset();

        try
        {
            await ConnectAsync(channel); //TODO: will timeout if doesn't have proper permissions in channel, check first.
            var bitrate = (channel as IVoiceChannel)?.Bitrate ?? 96000;
            await playerInterface.SpawnAsync(reqInfo, null);
            currentPlayer = playerInterface;
            using var output = (OpusEncodeStream)audioClient!.CreatePCMStream(AudioApplication.Music, bitrate, 1000, 0);
            await PlayNextAsync(channel, output);
        }
        finally
        {
            Log.Debug("Finished playing. Resetting state...");
            playLock.Set();
            await playerInterface.DisableAllButtonsAsync();
            await ResetState();
            if (audioClient is not null)
            {
                audioClient.ClientDisconnected -= OnClientDisconnect;
                audioClient = null;
            }
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
        await Queue.PutFirstAsync(await request.GetMediaAsync());
    }
}
