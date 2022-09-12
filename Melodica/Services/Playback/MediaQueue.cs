
using Melodica.Services.Media;
using Melodica.Utility;

namespace Melodica.Services.Playback;

public sealed class MediaQueue
{
    static readonly Random rng = new();

    private readonly object locker = new();

    private readonly List<LazyMedia> list = new();

    // Returns same media over and over.
    public bool Loop { get; set; }

    // Returns next media, putting the last at the end of the queue.
    public bool Repeat { get; set; }

    public bool Shuffle { get; set; }

    public bool IsEmpty => list.Count == 0;

    public int Length => list.Count;

    LazyMedia? lastMedia;

    public PlayableMedia this[int i] => list[i];

    public Task<TimeSpan> GetTotalDurationAsync()
    {
        return Task.FromResult(list.Sum(x => ((PlayableMedia)x).Info.Duration));
    }

    public async ValueTask<(TimeSpan duration, string? imageUrl)> GetQueueInfo()
    {
        return (await GetTotalDurationAsync(), ((PlayableMedia)list[0]).Info.ImageUrl);
    }

    public ValueTask EnqueueAsync(MediaCollection items)
    {
        lock (locker)
        {
            list.AddRange(items);
        }
        return ValueTask.CompletedTask;
    }

    public ValueTask PutFirstAsync(MediaCollection items)
    {
        lock (locker)
        {
            list.InsertRange(0, items);
        }
        return ValueTask.CompletedTask;
    }

    ValueTask<PlayableMedia> GetNextAsync()
    {
        lock (locker)
        {
            int index = Shuffle ? rng.Next(0, list.Count) : 0;
            LazyMedia? item = list[index];
            list.RemoveAt(index);
            lastMedia = item;
            return ValueTask.FromResult((PlayableMedia)item);
        }
    }

    public async ValueTask<PlayableMedia> DequeueAsync()
    {
        if (Loop && lastMedia is not null)
            return lastMedia;
        PlayableMedia? next = await GetNextAsync();
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

        LazyMedia item;
        lock (locker)
        {
            item = list[index];
            list.RemoveAt(index);
        }
        return ValueTask.FromResult((PlayableMedia)item);
    }

    public ValueTask<PlayableMedia> RemoveAtAsync(Index index)
    {
        lock (locker)
        {
            return RemoveAtAsync(index.GetOffset(list.Count - 1));
        }
    }
}
