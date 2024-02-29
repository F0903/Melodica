using System.Buffers;
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

    private readonly PlaybackStopwatch durationTimer = new();
    private readonly ManualResetEventSlim playLock = new(true);
    private CancellationTokenSource? cancellation;

    private IAudioClient? audioClient;
    private IAudioChannel? audioChannel;
    private bool skipRequested = false;
    private bool downloading = false;

    private JukeboxInterface? currentPlayer;

    private static readonly MemoryPool<byte> memory = MemoryPool<byte>.Shared;

    public MediaQueue Queue { get; } = new();
    public PlayableMedia? Song { get; private set; }

    public async Task SetPausedAsync(bool value)
    {
        if (!Playing) return;
        Paused = value;
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

    async Task WriteData(Stream input, OpusEncodeStream output, CancellationToken token)
    {
        bool BreakConditions() => token.IsCancellationRequested || skipRequested;

        void Pause()
        {
            durationTimer.Stop();
            while (Paused && !BreakConditions())
            {
                Thread.Sleep(2000);
            }
            durationTimer.Start();
        }

        const int frameBytes = 3840;
        using var memHandle = memory.Rent(frameBytes * 2);
        var buffer = memHandle.Memory;
        durationTimer.Start();
        await output.WriteSilentFramesAsync();
        try
        {
            while (await input.ReadAsync(buffer[..frameBytes], CancellationToken.None) != 0)
            {
                if (Paused)
                {
                    await output.WriteSilentFramesAsync();
                    Pause();
                    await output.WriteSilentFramesAsync();
                }

                if (BreakConditions())
                {
                    break;
                }

                await output.WriteAsync(buffer[..frameBytes], CancellationToken.None);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"Got exception when trying to write audio:\n{ex.Message}");
        }
        finally
        {
            await output.WriteSilentFramesAsync();
            await output.FlushAsync(CancellationToken.None);
        }
    }

    private async Task SendDataAsync(DataInfo data, OpusEncodeStream output, CancellationToken token)
    {
        async Task StartWrite()
        {
            try
            {
                var mediaPath = data.MediaPath ?? throw new NullReferenceException("MediaPath was not specified. (internal error)");
                using IAsyncAudioProcessor audio = data.Format == "s16le" ? new RawProcessor(mediaPath) : new FFmpegProcessor(mediaPath, data.Format);
                using var procStreams = await audio.ProcessAsync();
                await WriteData(procStreams.Output, output, token);
            }
            catch (OperationCanceledException ex) { }
            catch (Exception ex)
            {
                Log.Error(ex, $"SendDataAsync encountered an exception:\n{ex.Message}");
                throw;
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

        DataInfo dataInfo;
        try
        {
            dataInfo = await media.GetDataAsync();
        }
        catch
        {
            if (!Queue.IsEmpty) await PlayNextAsync(channel, output, token);
            else await DisconnectAsync();
            return;
        }

        await currentPlayer!.SetSongEmbedAsync(media.Info, media.CollectionInfo);

        var donePlaying = false;
        try
        {
            await SendDataAsync(dataInfo, output, token);
        }
        catch (Exception ex)
        {
            await DisconnectAsync();
            throw new CriticalException($"SendDataAsync encountered a fatal error. (please report)\n```{ex.Message}```");
        }
        finally
        {
            if (!Loop)
            {
                // If media is temporary (3rd party download) then delete the file.
                if (media is TempMedia temp)
                {
                    temp.DiscardTempMedia();
                }

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

        await Queue.EnqueueAsync(collection);
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

            await ConnectAsync(channel);
            var bitrate = (channel as IVoiceChannel)?.Bitrate ?? 96000;
            await player.SpawnAsync(reqInfo, collection.CollectionInfo);
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
