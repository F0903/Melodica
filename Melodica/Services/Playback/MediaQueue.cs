using Melodica.Services.Media;
using Melodica.Utility;

namespace Melodica.Services.Playback;

public sealed class MediaQueue
{
    static readonly Random rng = new();

    private readonly object locker = new();

    private readonly List<PlayableMedia> list = [];

    private PlayableMedia? lastDequeuedMedia = null;

    // Returns same media over and over.
    public bool Loop { get; set; }

    // Returns next media, putting the last at the end of the queue.
    public bool Repeat { get; set; }

    public bool Shuffle { get; set; }

    public bool IsEmpty => list.Count == 0;

    public int Length => list.Count;

    public PlayableMedia this[int i] => list[i];

    public Task<TimeSpan> GetTotalDurationAsync() => Task.FromResult(list.Sum(x => ((CachingPlayableMedia)x).Info.Duration));

    public async ValueTask<(TimeSpan duration, string? imageUrl)> GetQueueInfo() => (await GetTotalDurationAsync(), ((CachingPlayableMedia)list[0]).Info.ImageUrl);

    public ValueTask EnqueueAsync(PlayableMedia media)
    {
        lock (locker)
        {
            PlayableMedia? current = media;
            while(current is not null)
            {
                list.Add(current);
                current = current.Next;
            }
        }
        return ValueTask.CompletedTask;
    }

    public ValueTask PutFirstAsync(PlayableMedia media)
    {
        lock (locker)
        {
            PlayableMedia? current = media;
            int i = 0;
            while (current is not null)
            {
                list.Insert(i, current);
                current = current.Next;
                ++i;
            }
        }
        return ValueTask.CompletedTask;
    }

    ValueTask<PlayableMedia> GetNextAsync()
    {
        lock (locker)
        {
            var index = Shuffle ? rng.Next(0, list.Count) : 0;
            var item = list[index];
            lastDequeuedMedia = item;
            list.RemoveAt(index);
            return ValueTask.FromResult(item);
        }
    }

    public async ValueTask<PlayableMedia> DequeueAsync()
    {
        if (Loop && lastDequeuedMedia is not null)
            return lastDequeuedMedia;
        var next = await GetNextAsync();
        if (Repeat)
        {
            lock (locker)
            {
                list.Add(next);
            }
        }
        return next;
    }

    public ValueTask ClearAsync()
    {
        lock (locker)
        {
            list.Clear();
        }
        return ValueTask.CompletedTask;
    }

    public ValueTask<PlayableMedia> RemoveAtAsync(int index)
    {
        if (index < 0)
            throw new Exception("Index cannot be under 0.");
        else if (index > list.Count)
            throw new Exception("Index cannot be larger than the queues size.");

        PlayableMedia item;
        lock (locker)
        {
            item = list[index];
            list.RemoveAt(index);
        }
        return ValueTask.FromResult(item);
    }

    public ValueTask<PlayableMedia> RemoveAtAsync(Index index)
    {
        lock (locker)
        {
            return RemoveAtAsync(index.GetOffset(list.Count));
        }
    }
}
