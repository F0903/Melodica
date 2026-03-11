using System.Buffers;
using Discord;
using Discord.Audio;
using Discord.Audio.Streams;
using Discord.WebSocket;
using Melodica.Core.Exceptions;
using Melodica.Services.Audio;
using Melodica.Services.Caching;
using Melodica.Services.Media;
using Melodica.Services.Playback.Requests;
using Melodica.Utility.Extensions;
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
    private CancellationTokenSource? stopper;

    private IAudioClient? audioClient;
    private IAudioChannel? audioChannel;

    private JukeboxInterface? currentPlayerInterface;

    private readonly FFmpegProcessor mediaProcessor = new();

    public MediaQueue Queue { get; } = new();

    public MediaInfo? CurrentSong { get; set; }

    public async Task SetPausedAsync(bool value)
    {
        if (!Playing) return;
        Paused = value;
        mediaProcessor.SetPause(value);
        if (currentPlayerInterface is not null)
        {
            await currentPlayerInterface.SetButtonPressedAsync(JukeboxInterfaceButton.PlayPause, !value);
        }
    }

    public async Task SetLoopAsync(bool value)
    {
        Queue.Loop = value;
        if (currentPlayerInterface is not null)
        {
            await currentPlayerInterface.SetButtonPressedAsync(JukeboxInterfaceButton.Loop, value);
        }
    }

    public async Task SetShuffleAsync(bool value)
    {
        Queue.Shuffle = value;
        if (currentPlayerInterface is not null)
        {
            await currentPlayerInterface.SetButtonPressedAsync(JukeboxInterfaceButton.Shuffle, value);
        }
    }

    public async Task SetRepeatAsync(bool value)
    {
        Queue.Repeat = value;
        if (currentPlayerInterface is not null)
        {
            await currentPlayerInterface.SetButtonPressedAsync(JukeboxInterfaceButton.Repeat, value);
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
            if (node is not null) await node.CloseAllAsync();
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
            Log.Debug("No users left in voice channel. Disconnecting...");
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
        //TODO: update stream when connecting event fires. This should fix the disconnection problem
        audioClient.Connected += () =>
        {
            Console.WriteLine("DEBUG CONNECTED");
            return Task.CompletedTask;
        };
        audioClient.Disconnected += (ex) =>
        {
            Console.WriteLine($"DEBUG CONNECTED, EX: {ex}");
            return Task.CompletedTask;
        };
    }

    async Task SendDataAsync(PlayableMedia media, OpusEncodeStream output, CancellationToken cancellationToken)
    {
        try
        {
            durationTimer.Start();
            const int frameBytes = 3840;
            // Wrap the output stream to handle Dave encryption errors during initialization
            var wrappedOutput = new DaveErrorHandlingStream(output);
            await mediaProcessor.ProcessMediaAsync(
                media,
                wrappedOutput,
                async () =>
                {
                    durationTimer.Stop();
                    try
                    {
                        await output.WriteSilentFramesAsync();
                    }
                    catch (Exception ex) when (ex is TaskCanceledException || ex is OperationCanceledException)
                    {
                        Log.Debug("WriteSilentFramesAsync was cancelled.");
                    }
                    await output.FlushAsync();
                },
                () =>
                {
                    durationTimer.Start();
                },
                frameBytes,
                cancellationToken
            );
        }
        catch (OperationCanceledException)
        {
            Log.Debug("Sending data operation cancelled...");
        }
        finally
        {
            Log.Debug("Finished sending data...");
            durationTimer.Reset();
        }
    }

    async Task PlayNextAsync(IAudioChannel channel, OpusEncodeStream output)
    {
        if (Queue.IsEmpty && !Loop)
        {
            await DisconnectAsync();
            return;
        }

        var media = await Queue.DequeueAsync();
        CurrentSong = await media.GetInfoAsync();

        await currentPlayerInterface!.SetSongEmbedAsync(CurrentSong, null); //TODO: Consider reimplementing collectionInfo / playlist info again.

        if (Queue.Length > 1)
        {
            // If we have multiple songs queued, make sure the skip button is enabled.
            await currentPlayerInterface!.SetButtonEnabledAsync(JukeboxInterfaceButton.Skip, true);
        }

        try
        {
            stopper = new();
            var stopToken = stopper.Token;
            Log.Debug("Starting sending data..");
            await SendDataAsync(media, output, stopToken);
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
            await media.CloseAsync();

            if (Queue.Length <= 1)
            {
                await currentPlayerInterface!.SetButtonEnabledAsync(JukeboxInterfaceButton.Skip, false);
            }
        }

        await PlayNextAsync(channel, output);
    }

    public async Task<PlayResult> PlayAsync(IMediaRequest request, IAudioChannel channel, JukeboxInterface playerInterface)
    {
        var info = await request.GetInfoAsync();
        var media = await request.GetMediaAsync();

        await Queue.EnqueueAsync(media);

        if (Playing)
        {
            // If we are already playing, make sure the skip button is enabled and return a Queued result.
            await currentPlayerInterface!.SetButtonEnabledAsync(JukeboxInterfaceButton.Skip, true);
            return PlayResult.Queued;
        }

        playLock.Wait();
        playLock.Reset();

        try
        {
            await ConnectAsync(channel);
            var bitrate = (channel as IVoiceChannel)?.Bitrate ?? 96000;

            currentPlayerInterface = playerInterface;
            await currentPlayerInterface.SpawnAsync(info, null);
            using var output = (OpusEncodeStream)audioClient!.CreatePCMStream(AudioApplication.Music, bitrate, 1000, 0);
            await PlayNextAsync(channel, output);
        }
        finally
        {
            Log.Debug("Finished playing. Resetting state...");
            playLock.Set();
            await currentPlayerInterface!.DisableAllButtonsAsync();
            await ResetState();
            audioClient?.ClientDisconnected -= OnClientDisconnect;
            audioClient = null;
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
