using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Melodica.Services.Media;
using Melodica.Services.Playback.Requests;
using Melodica.Utility.Extensions;

namespace Melodica.Services.Playback
{
    public class MediaQueue
    {
        static readonly Random rng = new();

        private readonly object locker = new();

        private readonly List<PlayableMedia> list = new();

        // Returns same media over and over.
        public bool Loop { get; set; }

        // Returns next media, putting the last at the end of the queue.
        public bool Repeat { get; set; }

        public bool Shuffle { get; set; }

        public bool IsEmpty => list.Count == 0;

        public int Length => list.Count;

        public PlayableMedia this[int i] => list[i];

        public Task<TimeSpan> GetTotalDurationAsync() => Task.FromResult(list.Sum(x => x.Info.Duration));

        public PlayableMedia[] ToArray()
        {
            lock (locker)
                return list.ToArray();
        }

        public async ValueTask<MediaInfo> GetMediaInfoAsync() =>
            new MediaInfo()
            {
                Duration = await GetTotalDurationAsync(),
                ImageUrl = list[0].Info.ImageUrl
            };

        public ValueTask EnqueueAsync(MediaCollection items)
        {
            list.AddRange(items.GetMedia());
            return ValueTask.CompletedTask;
        }

        public ValueTask PutFirstAsync(MediaCollection items)
        {
            list.InsertRange(0, items.GetMedia());
            return ValueTask.CompletedTask;
        }

        ValueTask<PlayableMedia> GetNextAsync()
        {
            lock (locker)
            {
                int index = Shuffle ? rng.Next(0, list.Count) : 0;
                var item = list[index];
                return ValueTask.FromResult(item);
            }
        }

        public async ValueTask<PlayableMedia> DequeueAsync()
        {
            var next = await GetNextAsync();
            if (Repeat)
            {
                list.Add(next);
            }
            return next;
        }

        public ValueTask ClearAsync()
        {
            list.Clear();
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
            return RemoveAtAsync(index.GetOffset(list.Count - 1));
        }
    }
}