using Melodica.Services.Media;
using Melodica.Utility.Extensions;

namespace Melodica.Services.Playback;

// This is not exactly optimal in many ways.
public sealed class MediaQueue
{
    static readonly Random rng = new();

    private readonly Lock locker = new();

    private PlayableMedia? start;

    private PlayableMedia? lastDequeued;

    // Returns next media, putting the last at the end of the queue.
    public bool Repeat { get; set; }

    public bool Loop { get; set; }

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

    (PlayableMedia, int) GetLastNodeOf(PlayableMedia node)
    {
        using var _ = locker.EnterScope();

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

    public PlayableMedia GetAt(int index)
    {
        using var _ = locker.EnterScope();

        var current = start ?? throw new NullReferenceException("Start node was null.");
        for (var i = 0; i < index; i++)
        {
            current = current.Next ?? throw new NullReferenceException("Node at or before index was null.");
        }
        return current;
    }

    void InsertAt(PlayableMedia media, int index)
    {
        using var _ = locker.EnterScope();

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

    PlayableMedia RemoveAt(int index)
    {
        using var _ = locker.EnterScope();

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

    public ValueTask EnqueueAsync(PlayableMedia media)
    {
        using var _ = locker.EnterScope();
        InsertAt(media, Length);

        return ValueTask.CompletedTask;
    }

    public ValueTask PutFirstAsync(PlayableMedia media)
    {
        using var _ = locker.EnterScope();
        InsertAt(media, 0);
        return ValueTask.CompletedTask;
    }

    public ValueTask<PlayableMedia> DequeueAsync()
    {
        using var _ = locker.EnterScope();
        
        if (Loop && lastDequeued is not null)
        {
            return lastDequeued.WrapValueTask();
        }

        var index = Shuffle ? rng.Next(0, Length) : 0;
        var dequeued = Repeat ? GetAt(index) : RemoveAt(index);

        lastDequeued = dequeued;

        return dequeued.WrapValueTask();
    }

    /// <returns>The starting node.</returns>
    public ValueTask<PlayableMedia?> ClearAsync()
    {
        using var _ = locker.EnterScope();

        if (start is null) return default;
        var original = start;

        // Unsure if this is enough to GC all the nodes
        start = null;
        lastDequeued = null;
        Length = 0;
        return original.WrapValueTask<PlayableMedia?>();
    }

    public ValueTask<PlayableMedia> RemoveAtAsync(int index)
    {
        using var _ = locker.EnterScope();
        return RemoveAt(index).WrapValueTask();
    }

    public ValueTask<PlayableMedia> RemoveAtAsync(Index index)
    {
        using var _ = locker.EnterScope();
        return RemoveAtAsync(index.GetOffset(Length));
    }
}
