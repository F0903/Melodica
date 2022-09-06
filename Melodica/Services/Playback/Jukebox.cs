
using Discord;
using Discord.Audio;
using Discord.WebSocket;

using Melodica.Core.Exceptions;
using Melodica.Services.Audio;
using Melodica.Services.Downloaders.Exceptions;
using Melodica.Services.Media;
using Melodica.Services.Playback.Requests;
using Melodica.Utility.Extensions;

namespace Melodica.Services.Playback;

readonly struct AloneTimerState
{
    public readonly Func<ValueTask> Stop { get; init; }
    public readonly IAudioChannel Channel { get; init; }
}

public class Jukebox
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
        Error,
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

    static void SendSilence(AudioOutStream output, int frames = 100)
    {
        const int channels = 2;
        const int bits = 16;
        const int blockAlign = channels * bits;
        int bytes = blockAlign * frames;
        Span<byte> buffer = bytes < 1024 ? stackalloc byte[bytes] : new byte[bytes];
        output.Write(buffer);
        output.Flush();
    }

    void WriteData(AudioProcessor audio, AudioOutStream output, CancellationToken token)
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
        const int BUFSIZE = 4 * 1024;
        Span<byte> buffer = new byte[BUFSIZE];
        Stream? input = audio.GetOutput();
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

            output.Write(buffer[0..count]);
        }
        SendSilence(output);
    }

    private Task<bool> SendDataAsync(AudioProcessor audio, AudioOutStream output, IAudioChannel channel, CancellationToken token)
    {
        bool aborted = false;

        void StartWrite()
        {
            try
            {
                AloneTimerState timerState = new() { Stop = StopAsync, Channel = channel };
                using Timer? aloneTimer = new(static async state =>
                {
                    //TODO: Fix
                    AloneTimerState ts = (AloneTimerState)(state ?? throw new NullReferenceException("AloneTimer state parameter cannot be null."));
                    if (await CheckIfAloneAsync(ts.Channel))
                        await ts.Stop();
                }, timerState, 0, 30000);

                WriteData(audio, output, token);
            }
            catch (Exception)
            {
                aborted = true;
            }
            finally
            {
                durationTimer.Reset();
            }
        }

        Thread? writeThread = new(StartWrite)
        {
            IsBackground = false,
            Priority = ThreadPriority.Highest
        };

        writeThread.Start();
        writeThread.Join(); // Thread exit

        if (skipRequested)
            skipRequested = false;

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
    }

    async Task PlayNextAsync(IAudioChannel channel, AudioOutStream output, CancellationToken token, TimeSpan? startingPoint = null)
    {
        using FFmpegAudioProcessor? audio = new();
        PlayableMedia? media = await queue.DequeueAsync();
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
        await audio.StartProcess(dataInfo, startingPoint); 

        bool faulted = await SendDataAsync(audio, output, channel, token);

        // If media is temporary (3rd party download) then delete the file.
        if ((!Loop || faulted) && media is TempMedia temp)
        {
            // Dispose first so ffmpeg releases file handles.
            audio.Dispose();
            await audio.WaitForExit();
            temp.DiscardTempMedia();
        }

        if (faulted)
        {
            throw new CriticalException("SendDataAsync encountered a fatal error. (dbg-msg)");
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
            return PlayResult.Error;
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
