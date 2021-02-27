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
        private readonly object locker = new object();

        private readonly List<MediaRequest> list = new List<MediaRequest>();

        public bool IsEmpty => list.Count == 0;

        public int Length => list.Count;

        public MediaRequest this[int i] => list[i];

        public TimeSpan GetTotalDuration() => list.Sum(x => x.GetInfo().Duration);

        public MediaRequest[] ToArray()
        {
            lock (locker)
                return list.ToArray();
        }

        public MediaInfo GetMediaInfo() => new MediaInfo() { Duration = GetTotalDuration(), Image = list[0].GetInfo().Image };

        public Task EnqueueAsync(MediaRequest item)
        {
            list.Add(item);
            return Task.CompletedTask;
        }

        public Task EnqueueAsync(IEnumerable<MediaRequest> items)
        {
            list.AddRange(items);
            return Task.CompletedTask;
        }

        public Task PutFirstAsync(MediaRequest item)
        {
            list.Insert(0, item);
            return Task.CompletedTask;
        }

        public Task PutFirstAsync(IEnumerable<MediaRequest> items)
        {
            list.InsertRange(0, items);
            return Task.CompletedTask;
        }

        public Task<MediaRequest> DequeueRandomAsync(bool keep = false)
        {
            var rng = new Random();
            MediaRequest item;
            lock (locker)
            {
                item = list[rng.Next(0, list.Count)];
                if (!keep) list.Remove(item);
            }
            return Task.FromResult(item);
        }

        public Task<MediaRequest> DequeueAsync(bool keep = false)
        {
            MediaRequest item;
            lock (locker)
            {
                item = list[0];
                if (!keep) list.Remove(item);
            }
            return Task.FromResult(item);
        }

        public Task ClearAsync()
        {
            list.Clear();
            return Task.CompletedTask;
        }

        public Task<MediaRequest> RemoveAtAsync(int index)
        {
            if (index < 0)
                throw new Exception("Index cannot be under 0.");
            else if (index > list.Count)
                throw new Exception("Index cannot be larger than the queues size.");

            MediaRequest item;
            lock (locker)
            {
                item = list[index];
                list.RemoveAt(index);
            }
            if (item.ParentRequestInfo != null) // Subtract duration.
                item.ParentRequestInfo.Duration -= item.GetInfo().Duration;
            return Task.FromResult(item);
        }

        public Task<MediaRequest> RemoveAtAsync(Index index)
        {
            return RemoveAtAsync(index.GetOffset(list.Count - 1));
        }
    }
}