using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Suits.Utility.Extensions;
using Suits.Jukebox.Models.Requests;

namespace Suits.Jukebox.Models
{
    public class SongQueue
    {
        private readonly object locker = new object();

        private readonly List<MediaRequest> list = new List<MediaRequest>();

        public bool IsEmpty { get => list.Count == 0; }

        public int Length { get => list.Count; }

        public MediaRequest this[int i]
        {
            get => list[i];
        }

        public TimeSpan GetTotalDuration() => list.Sum(x => x.GetInfo().Duration);

        public MediaRequest[] ToArray()
        {
            lock (locker)
                return list.ToArray();
        }

        public MediaMetadata GetMediaInfo() => new MediaMetadata() { Duration = GetTotalDuration(), Thumbnail = list[0].GetInfo().Thumbnail };

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

        public Task PutFirst(MediaRequest item)
        {
            list.Insert(0, item);
            return Task.CompletedTask;
        }

        public Task PutFirst(IEnumerable<MediaRequest> items)
        {
            list.InsertRange(0, items);
            return Task.CompletedTask;
        }

        public Task<MediaRequest> DequeueRandomAsync()
        {
            var rng = new Random();
            MediaRequest item;
            lock (locker)
            {
                item = list[rng.Next(0, list.Count)];
                list.Remove(item);
            }
            return Task.FromResult(item);
        }

        public Task<MediaRequest> DequeueAsync(bool keep = false)
        {
            MediaRequest item;
            lock (locker)
            {
                item = list[0];
                if(!keep) list.Remove(item);
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
            return Task.FromResult(item);
        }
    }
}