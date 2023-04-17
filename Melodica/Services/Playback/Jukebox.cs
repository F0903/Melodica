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

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

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

    private JukeboxInterface? currentPlayer;
    private PlayableMedia? currentSong;

    public async Task SetPaused(bool value)
    {
        if (!Playing) return;
        Paused = value;
        if (currentPlayer is not null)
        {
            await currentPlayer.SetButtonStateAsync(JukeboxInterfaceButton.PlayPause, !value);
        }
    }

    public async Task SetLoop(bool value)
    {
        queue.Loop = value;
        if (currentPlayer is not null)
        {
            await currentPlayer.SetButtonStateAsync(JukeboxInterfaceButton.Loop, value);
        }
    }

    public async Task SetShuffle(bool value)
    {
        queue.Shuffle = value;
        if (currentPlayer is not null)
        {
            await currentPlayer.SetButtonStateAsync(JukeboxInterfaceButton.Shuffle, value);
        }
    }

    public async Task SetRepeat(bool value)
    {
        queue.Repeat = value;
        if (currentPlayer is not null)
        {
            await currentPlayer.SetButtonStateAsync(JukeboxInterfaceButton.Repeat, value);
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

    //TODO: Implement sending silent frames in Discord.Net library
    async Task WriteData(Stream input, OpusEncodeStream output, CancellationToken token)
    {
        bool BreakConditions() => token.IsCancellationRequested || skipRequested;

        void Pause()
        {
            durationTimer.Stop();
            while (Paused && !BreakConditions())
            {
                Thread.Sleep(1000);
            }
            durationTimer.Start();
        }

        int count;
        var buffer = new byte[5 * 1024];
        durationTimer.Start();
        try
        {
            while ((count = await input.ReadAsync(buffer, token)) != 0)
            {
                if (Paused)
                {
                    await output.WriteSilentFramesAsync();
                    Pause();
                    await output.WriteSilentFramesAsync();
                }

                if (BreakConditions())
                {
                    if (token.IsCancellationRequested)
                        return;
                    break;
                }

                await output.WriteAsync(buffer, token);
            }
        }
        finally
        {
            await output.WriteSilentFramesAsync();
        }
    }

    private Task<bool> SendDataAsync(DataInfo data, OpusEncodeStream output, CancellationToken token)
    {
        var aborted = false;

        async Task StartWrite()
        {
            try
            {
                var mediaPath = data.MediaPath ?? throw new NullReferenceException("MediaPath was not specified. (internal error)");
                using IAsyncAudioProcessor audio = data.Format == "s16le" ? new RawProcessor(mediaPath) : new FFmpegProcessor(mediaPath, data.Format);
                using var input = await audio.ProcessAsync();
                await WriteData(input, output, token);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Log.Error(ex, "SendDataAsync encountered an exception.");
                aborted = true;
            }
            finally
            {
                durationTimer.Reset();
            }
        }

        // Start a new manual thread so that priority can be set.
        Thread writeThread = new(StartWrite().Wait)
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
        if (cancellation is null || cancellation.IsCancellationRequested) return;

        await ClearAsync();
        cancellation.Cancel();
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
            IReadOnlyCollection<IUser> users = audioChannel switch
            {
                SocketVoiceChannel svc => svc.ConnectedUsers, // Actually accurate.
                _ => (IReadOnlyCollection<IUser>)await audioChannel.GetUsersAsync().FlattenAsync() // Will probably report wrong due to caching.
            };

            if (!users.IsOverSize(1))
            {
                await StopAsync();
            }
        };
    }

    async Task PlayNextAsync(IAudioChannel channel, OpusEncodeStream output, CancellationToken token)
    {
        var media = await queue.DequeueAsync() ?? throw new CriticalException("Song from queue was null.");
        currentSong = media;

        DataInfo dataInfo;
        try
        {
            dataInfo = await media.GetDataAsync();
        }
        catch
        {
            if (!queue.IsEmpty) await PlayNextAsync(channel, output, token);
            else await DisconnectAsync();
            return;
        }

        await currentPlayer!.SetSongEmbedAsync(media.Info, media.CollectionInfo);
        var faulted = await SendDataAsync(dataInfo, output, token);

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
            var token = cancellation.Token;

            await ConnectAsync(channel);
            var bitrate = (channel as IVoiceChannel)?.Bitrate ?? 96000;
            await player.SpawnAsync(reqInfo, collection.CollectionInfo);
            currentPlayer = player;
            using var output = (OpusEncodeStream)audioClient!.CreatePCMStream(AudioApplication.Music, bitrate, 200, 0);
            await PlayNextAsync(channel, output, token);
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
        var media = await request.GetMediaAsync();
        await queue.PutFirstAsync(media);
    }
}
