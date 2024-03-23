using Melodica.Services.Media;
using Melodica.Services.Playback.Requests;
using Melodica.Utility;

namespace Melodica.Services.Playback;

public sealed class MediaQueue
{
    static readonly Random rng = new();

    private readonly object locker = new();

    private PlayableMediaStream? start;

    // Returns next media, putting the last at the end of the queue.
    public bool Repeat { get; set; }

    public bool Shuffle { get; set; }

    public int Length { get; private set; }

    public bool IsEmpty => Length == 0;

    public async ValueTask<(TimeSpan duration, string? imageUrl)> GetQueueInfo() => (await GetTotalDurationAsync(), (await start!.GetInfoAsync()).ImageUrl);

    public async ValueTask<TimeSpan> GetTotalDurationAsync()
    {
        if (start is null) return TimeSpan.Zero;
        TimeSpan time = TimeSpan.Zero;
        var current = start;
        while (current is not null)
        {
            var info = await current.GetInfoAsync();
            time += info.Duration;
            current = current.Next;
        }
        return time;
    }

    static (PlayableMediaStream, int) GetLastNodeOf(PlayableMediaStream node)
    {
        int count = 0;
        var lastNode = node;
        while (true)
        {
            ++count;
            var nextNode = lastNode.Next;
            if (nextNode is not null) lastNode = nextNode;
            else break;
        }
        return (lastNode, count);
    }

    public PlayableMediaStream GetAt(int index)
    {
        var current = start ?? throw new NullReferenceException("Start node was null.");
        for (var i = 0; i < index; i++)
        {
            current = current.Next ?? throw new NullReferenceException("Node at or before index was null.");
        }
        return current;
    }

    void InsertAt(PlayableMediaStream media, int index)
    {
        if (index == 0)
        {
            var original = start;
            start = media;
            var (lastNodeStart, countStart) = GetLastNodeOf(media);
            lastNodeStart.Next = original;
            Length += countStart;
            return;
        }

        var beforeIndex = GetAt(index - 1);
        var prevIndexNode = beforeIndex.Next;
        beforeIndex.Next = media;

        var (lastNode, count) = GetLastNodeOf(media);
        lastNode.Next = prevIndexNode;
        Length += count;
    }

    PlayableMediaStream RemoveAt(int index)
    {
        if (index == 0)
        {
            var original = start ?? throw new NullReferenceException("Start node was null.");
            var next = original.Next;
            Length -= 1;
            if (next is null)
            {
                start = null;
                return original;
            }
            start = next;
            return original;
        }

        var beforeIndex = GetAt(index - 1);
        var indexNode = beforeIndex.Next ?? throw new NullReferenceException("Index node was null.");
        var afterIndex = indexNode.Next;

        beforeIndex.Next = afterIndex;
        Length -= 1;
        return indexNode;
    }

    public ValueTask EnqueueAsync(PlayableMediaStream media)
    {
        lock (locker)
        {
            InsertAt(media, Length);
        }
        return ValueTask.CompletedTask;
    }

    public ValueTask PutFirstAsync(PlayableMediaStream media)
    {
        lock (locker)
        {
            InsertAt(media, 0);
        }
        return ValueTask.CompletedTask;
    }

    public ValueTask<PlayableMediaStream> DequeueAsync()
    {
        lock (locker)
        {
            var index = Shuffle ? rng.Next(0, Length) : 0;
            var dequeued = Repeat ? GetAt(index) : RemoveAt(index);
            return dequeued.WrapValueTask();
        }
    }

    /// <returns>The starting node.</returns>
    public ValueTask<PlayableMediaStream?> ClearAsync()
    {
        if (start is null) return default;
        var original = start;
        lock (locker)
        {
            //Note: unsure if this is enough to garbage collect the nodes.
            start = null;
            Length = 0;
        }
        return original.WrapValueTask<PlayableMediaStream?>();
    }

    public ValueTask<PlayableMediaStream> RemoveAtAsync(int index)
    {
        return RemoveAt(index).WrapValueTask();
    }

    public ValueTask<PlayableMediaStream> RemoveAtAsync(Index index)
    {
        lock (locker)
        {
            return RemoveAtAsync(index.GetOffset(Length));
        }
    }
}
