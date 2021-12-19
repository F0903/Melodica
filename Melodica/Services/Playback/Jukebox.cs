
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

public enum MediaState
{
    Error,
    Queued,
    Downloading,
    Playing,
    Finished
};

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

    public delegate ValueTask MediaCallback(MediaInfo info, MediaInfo? playlistInfo, MediaState state);

    private bool paused;
    public bool Paused
    {
        get => paused;
        set
        {
            if (!Playing) return;
            paused = value;
        }
    }

    public bool Playing => !playLock.IsSet;

    public bool Loop { get => queue.Loop; set => queue.Loop = value; }

    public bool Shuffle { get => queue.Shuffle; set => queue.Shuffle = value; }

    public bool Repeat { get => queue.Repeat; set => queue.Repeat = value; }

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

    private PlayableMedia? currentSong;

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
        IReadOnlyCollection<IUser>? users = await channel.GetUsersAsync().FirstAsync();
        if (!users.IsOverSize(1))
            return true;
        else return false;
    }

    static int GetChannelBitrate(IAudioChannel channel)
    {
        return channel switch
        {
            SocketVoiceChannel sv => sv.Bitrate,
            _ => throw new CriticalException("Unable to get channel bitrate.")
        };
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

        int count = 0;
        Span<byte> buffer = stackalloc byte[1024];
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
                SendSilence(output);
                break;
            }

            output.Write(buffer[0..count]);
        }
        SendSilence(output);
    }

    private Task<bool> SendDataAsync(AudioProcessor audio, AudioOutStream output, IAudioChannel channel, CancellationToken token)
    {
        bool abort = false;

        void StartWrite()
        {
            try
            {
                AloneTimerState timerState = new AloneTimerState() { Stop = StopAsync, Channel = channel };
                using Timer? aloneTimer = new Timer(static async state =>
                {
                    AloneTimerState ts = (AloneTimerState)(state ?? throw new NullReferenceException("AloneTimer state parameter cannot be null."));
                    if (await CheckIfAloneAsync(ts.Channel))
                        await ts.Stop();
                }, timerState, 0, 20000);

                WriteData(audio, output, token);
            }
            catch (Exception)
            {
                abort = true;
            }
            finally
            {
                durationTimer.Reset();
            }
        }

        Thread? writeThread = new Thread(StartWrite)
        {
            IsBackground = false,
            Priority = ThreadPriority.Highest
        };

        writeThread.Start();
        writeThread.Join(); // Thread exit

        if (skipRequested)
            skipRequested = false;

        return Task.FromResult(abort); // Returns true if an error occured.
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

        Loop = false;
        await ClearAsync();
        cancellation!.Cancel();
        playLock.Wait(5000);
    }

    public ValueTask SkipAsync()
    {
        skipRequested = true;
        return ValueTask.CompletedTask;
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
        PlaybackStatusHandler status = new(callbackChannel);
        using FFmpegAudioProcessor? audio = new FFmpegAudioProcessor();
        PlayableMedia? media = await queue.DequeueAsync();
        if (media is null)
            throw new CriticalException("Song from queue was null.");

        currentSong = media;

        if (!Loop)
            await status.Send(media.Info, media.CollectionInfo, MediaState.Downloading);

        DataInfo dataInfo;
        try
        {
            dataInfo = await media.GetDataAsync();
        }
        catch (Exception ex)
        {
            await status.RaiseError($"Could not get media.\nError: ``{ex.Message}``");
            if (!queue.IsEmpty) await PlayNextAsync(channel, output, token, null);
            else await DisconnectAsync();
            return;
        }

        await audio.StartProcess(dataInfo, startingPoint);

        if (!Loop)
            await status.SetPlaying();

        //TESTING TO SEE IF THIS GETS CALLED ON RANDOM DISCONNECTS
        audioClient!.Disconnected += async (ex) =>
        {
            await DisconnectAsync();
        };

        bool faulted = await SendDataAsync(audio, output, channel, token);

        if ((!Loop || faulted) && media is TempMedia temp)
        {
            // Dispose first so ffmpeg releases file handles.
            audio.Dispose();
            await audio.WaitForExit();
            temp.DiscardTempMedia();
        }

        if (!Loop || faulted)
            await status.SetFinished();

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

    public async Task PlayAsync(IMediaRequest request, IAudioChannel channel, TimeSpan? startingPoint = null)
    {
        if (downloading)
            return;

        PlaybackStatusHandler status = new(callbackChannel);

        MediaInfo reqInfo;
        MediaCollection collection;
        try
        {
            downloading = true;
            reqInfo = await request.GetInfoAsync();
            collection = await request.GetMediaAsync();
            downloading = false;
        }
        catch (Exception ex)
        {
            downloading = false;
            string? msg = ex is MediaUnavailableException ? "Media is unavailable." : "Unknown error occurred during download of media.";
            await status.RaiseError(msg);
            return;
        }

        await queue.EnqueueAsync(collection);
        if (Playing)
        {
            await status.Send(reqInfo, collection.CollectionInfo, MediaState.Queued);
            return;
        }

        playLock.Wait();
        playLock.Reset();
        try
        {
            if (reqInfo.MediaType == MediaType.Playlist)
                await status.Send(reqInfo, collection.CollectionInfo, MediaState.Playing);

            cancellation = new();
            CancellationToken token = cancellation.Token;
            await ConnectAsync(channel);
            int bitrate = GetChannelBitrate(channel);
            using AudioOutStream? output = audioClient!.CreatePCMStream(AudioApplication.Music, bitrate, 200, 0);
            await PlayNextAsync(channel, output, token, startingPoint);

            if (reqInfo.MediaType == MediaType.Playlist)
                await status.SetFinished();
        }
        finally
        {
            playLock.Set();
        }
    }

    public async Task SwitchAsync(IMediaRequest request)
    {
        Loop = false;
        Shuffle = false;
        await SetNextAsync(request);
        await SkipAsync();
    }

    public async Task SetNextAsync(IMediaRequest request)
    {
        MediaCollection? media = await request.GetMediaAsync();
        await queue.PutFirstAsync(media);
    }
}
