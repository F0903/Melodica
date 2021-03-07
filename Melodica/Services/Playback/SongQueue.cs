using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Melodica.Services.Media;
using Melodica.Services.Playback.Requests;
using Melodica.Utility.Extensions;

namespace Melodica.Services.Playback
{
    public class SongQueue
    {
        private readonly object locker = new();

        private readonly List<PlayableMedia> list = new();

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

        public ValueTask<PlayableMedia> DequeueRandomAsync(bool keep = false)
        {
            var rng = new Random();
            PlayableMedia item;
            lock (locker)
            {
                item = list[rng.Next(0, list.Count)];
                if (!keep) list.Remove(item);
            }
            return ValueTask.FromResult(item);
        }

        public ValueTask<PlayableMedia> DequeueAsync(bool keep = false)
        {
            PlayableMedia item;
            lock (locker)
            {
                item = list[0];
                if (!keep) list.Remove(item);
            }
            return ValueTask.FromResult(item);
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